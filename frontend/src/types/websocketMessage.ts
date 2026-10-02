export interface WebSocketMessage {
        type: number;
        channel: string | null; 
        payload?: any| null;
}