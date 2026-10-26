import type { WebSocketService } from "@/services/common/websockets/WebSocketService";
import webSocketService from '@/services/common/websockets/WebSocketService';
import { WSMessageType, type ChatChannel, type WebSocketMessage, type WSTicketRespone } from "@/types";
import { ref, watch } from "vue";
import { newId } from "../utils";
import { post } from "../api/ApiService";

type Pending = {
    resolve: (m: WebSocketMessage) => void;
    reject: (e: Error) => void;
    timer: ReturnType<typeof setTimeout>;
};

export type AckPromise = {
    id: string,
    promise: Promise<WebSocketMessage>
};

const wssUrl = import.meta.env.VITE_WSS_URL;

export class WebSocketMessageService {
    private webSocketService: WebSocketService = webSocketService;
    public connected = ref(false);
    public isActive = false;

    private subscriptions: Record<string, (payload: WebSocketMessage) => void> = {};
    private pendingSubscriptions: Record<string, (payload: WebSocketMessage) => void> = {};
    private pending = new Map<string, Pending>();

    async connect(onOnpen: () => Promise<void>) {
        this.isActive = true;
        if (this.webSocketService.isConnected()) {
            await onOnpen();
            return;
        }

        const { ticket } = await post<WSTicketRespone>('auth/ws-ticket');

        const url = new URL(wssUrl)
        url.searchParams.set('ticket', ticket)

        this.webSocketService.connect(url, {
            onOpen: async () => { this.connected.value = true; await onOnpen(); },
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

    disconnect() {
        this.webSocketService.close();
    }

    onClose() {
        this.connected.value = false;
        // fail everything still waiting, so nothing hangs forever
        for (const [id, p] of this.pending) {
            clearTimeout(p.timer);
            p.reject(new Error('Connection closed'));
            this.pending.delete(id);
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

    sendMessage(chatChannel: ChatChannel, payload: any, timeoutMs = 5000): AckPromise {
        const message: WebSocketMessage = { channel: chatChannel.id, payload: JSON.stringify(payload), type: WSMessageType.ChatMessage };
        return this.sendWithAck(message, timeoutMs);
    }

    subscribe(channel: string, callback: (payload: WebSocketMessage) => void) {
        this.subscriptions[channel] = callback
        const message: WebSocketMessage = { channel, type: WSMessageType.Subscribe };
        this.webSocketService.send(JSON.stringify(message));
    }

    unsubscribe(channel: string, callback: (payload: WebSocketMessage) => void) {
        delete this.subscriptions[channel]
        const message: WebSocketMessage = { channel, type: WSMessageType.Unsubscribe };
        this.webSocketService.send(JSON.stringify(message));
    }

    onMessage(message: WebSocketMessage) {
        const key = message.channel;
        this.subscriptions[key!]?.(message);
    }

    addPendingSubscription(channel: string, callback: (payload: WebSocketMessage) => void): void {
        this.pendingSubscriptions[channel] = callback
    }

    subscribePendingSubscriptions(): void {
        for (const channel of Object.keys(this.subscriptions)) {
            const message: WebSocketMessage = { channel, type: WSMessageType.Subscribe };
            this.webSocketService.send(JSON.stringify(message));
            delete this.pendingSubscriptions[channel];
        }

        for (const channel of Object.keys(this.pendingSubscriptions)) {
            const message: WebSocketMessage = { channel, type: WSMessageType.Subscribe };
            this.webSocketService.send(JSON.stringify(message));
        }
        this.pendingSubscriptions = {};
    }

    async tryReconnect(): Promise<void> {
        if (!this.connected.value) {
            await this.connect(async () => {
                this.subscribePendingSubscriptions();
            });
        }
    }
}

const wsMessageService = new WebSocketMessageService();
const recconnectInterval = 3000;

let reconnectTimer: number | undefined = undefined;

watch(wsMessageService.connected, async (connected) => {
    if (connected || !wsMessageService.isActive) {
        if (reconnectTimer) {
            clearInterval(reconnectTimer);
            reconnectTimer = undefined;
        }
        return;
    }

    if (!reconnectTimer) {
        await wsMessageService.tryReconnect();
        reconnectTimer = setInterval(async () => {
            await wsMessageService.tryReconnect();
        }, recconnectInterval);
    }
});

export default wsMessageService;