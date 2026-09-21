export interface WebSocketHandlers {
    onOpen?: (event: Event) => void;
    onClose?: (event: Event) => void;
    onError?: (event: Event) => void;
    onMessage?: (event: MessageEvent) => void;
}

export class WebSocketService {
    private socket: WebSocket | null = null;

    connect(url: string, handlers: WebSocketHandlers = {}): void {
        if (this.socket != null) { //Close old socket
            this.close();
        }

        this.socket = new WebSocket(url);
        this.socket.onopen = (event) => handlers.onOpen?.(event);
        this.socket.onclose = (event) => handlers.onClose?.(event);
        this.socket.onerror = (event) => handlers.onError?.(event);
        this.socket.onmessage = (event) => handlers.onMessage?.(event);
    }

    send(data: string): void {
        if (!this.socket || this.socket.readyState !== WebSocket.OPEN) {
            throw new Error('WebSocket is not connected');
        }
        this.socket.send(data);
    }

    close(): void {
        this.socket?.close();
        this.socket = null;
    }
}

export default new WebSocketService();