import type { WebSocketService } from "@/services/common/websockets/WebSocketService";
import webSocketService from '@/services/common/websockets/WebSocketService'; 
import type { WebSocketMessage } from "@/types";

export class WebSocketMessageService {
    private webSocketService: WebSocketService = webSocketService;
    private wsStatus: any = {value : ""};

    private subscriptions: Record<string, (payload: WebSocketMessage) => void> = {};

    connect (onOnpen: () => void) {

        if (this.webSocketService.isConnected()){
            return;
        }

        this.webSocketService.connect(import.meta.env.VITE_WSS_URL, {
        onOpen: () => { onOnpen(); },
        onClose: () => { this.wsStatus.value = 'Connection Closed'; },
        onError: () => { this.wsStatus.value = 'Connection Error'; },
        onMessage: (event) => { 
            const message = JSON.parse(event.data) as WebSocketMessage;
            this.onMessage(message);
         },
    });
    }

    sendMessage(channel: string, payload: any) {
        const message: WebSocketMessage = {channel, payload :JSON.stringify(payload), type: 3};
        this.webSocketService.send(JSON.stringify(message));
    }

    subscribe(channel: string, callback: (payload:WebSocketMessage) => void) {
        this.subscriptions[channel] = callback
        const message: WebSocketMessage = {channel, type: 1};
        this.webSocketService.send(JSON.stringify(message));
    }

    onMessage(message: WebSocketMessage) {
        const key = message.channel;
        this.subscriptions[key!]?.(message);
    } 
}
const wsMessageService = new WebSocketMessageService() 
export default wsMessageService;