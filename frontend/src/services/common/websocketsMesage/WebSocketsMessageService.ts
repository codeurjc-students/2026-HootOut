import type { WebSocketService } from "@/services/common/websockets/WebSocketService";
import webSocketService from '@/services/common/websockets/WebSocketService';
import { WSMessageType, type WebSocketMessage } from "@/types";
import { ref } from "vue";
import { newId } from "../utils";

type Pending = {
    resolve: (m: WebSocketMessage) => void;
    reject: (e: Error) => void;
    timer: ReturnType<typeof setTimeout>;
};

export type AckPromise = {
    id: string,
    promise: Promise<WebSocketMessage>
};

export class WebSocketMessageService {
    private webSocketService: WebSocketService = webSocketService;
    private connected = ref(false);

    private subscriptions: Record<string, (payload: WebSocketMessage) => void> = {};

    private pending = new Map<string, Pending>();

    connect(onOnpen: () => void) {

        if (this.webSocketService.isConnected()) {
            return;
        }

        this.webSocketService.connect(import.meta.env.VITE_WSS_URL, {
            onOpen: () => { this.connected.value = true; onOnpen(); },
            onClose: () => this.onClose(),
            onError: () => this.onClose(),
            onMessage: (event) => {
                const message = JSON.parse(event.data) as WebSocketMessage;
                if (!this.checkAck(message)) {
                    this.onMessage(message);
                }
            },
        });
    }

    onClose() {
        {
            this.connected.value = false;
            // fail everything still waiting, so nothing hangs forever
            for (const [id, p] of this.pending) {
                clearTimeout(p.timer);
                p.reject(new Error('Connection closed'));
                this.pending.delete(id);
            }
        }
    }

    checkAck(message: WebSocketMessage): boolean {
        if (message.replyTo && this.pending.has(message.replyTo)) {
            const p = this.pending.get(message.replyTo)!;
            clearTimeout(p.timer);
            this.pending.delete(message.replyTo);

            if (message.type === WSMessageType.Error) {
                p.reject(new Error('Server errer'))
            } else {
                p.resolve(message)
                return true;
            }
        }
        return false;
    }

    sendWithAck(message: WebSocketMessage, timeoutMs = 5000): any {
        const id = newId();
        message.id = id;

        return {
            id: id,
            promise: new Promise((resolve, reject) => {
                const timer = setTimeout(() => {
                    this.pending.delete(id);
                    reject(new Error(`No ack for ${message.type} within ${timeoutMs}ms`));
                }, timeoutMs);

                this.pending.set(id, { resolve, reject, timer });
                this.webSocketService.send(JSON.stringify(message));
            })
        };
    }

    sendMessage(channel: string, payload: any, timeoutMs = 5000): AckPromise {
        const message: WebSocketMessage = { channel, payload: JSON.stringify(payload), type: WSMessageType.ChatMessage };
        return this.sendWithAck(message, timeoutMs);
    }

    subscribe(channel: string, callback: (payload: WebSocketMessage) => void) {
        this.subscriptions[channel] = callback
        const message: WebSocketMessage = { channel, type: WSMessageType.Subscribe };
        this.webSocketService.send(JSON.stringify(message));
    }

    onMessage(message: WebSocketMessage) {
        const key = message.channel;
        this.subscriptions[key!]?.(message);
    }
}
const wsMessageService = new WebSocketMessageService()
export default wsMessageService;