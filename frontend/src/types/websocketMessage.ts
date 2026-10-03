export interface WebSocketMessage {
        id?: string,
        replyTo?: string,
        type: number;
        channel: string | null;
        payload?: any | null;
}

export enum WSMessageType {
        Invalid = 0,
        Subscribe = 1,
        Unsubscribe = 2,
        ChatMessage = 3,
        Ack = 4,
        Error = 5
}