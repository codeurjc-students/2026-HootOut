// src/services/websockets/WebSocketService.test.ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { WebSocketService } from './WebSocketService.ts';

class FakeWebSocket {
    static instances: FakeWebSocket[] = [];
    static readonly OPEN = 1;

    url: string;
    readyState = FakeWebSocket.OPEN;
    onopen: ((event: Event) => void) | null = null;
    onclose: ((event: Event) => void) | null = null;
    onerror: ((event: Event) => void) | null = null;
    onmessage: ((event: MessageEvent) => void) | null = null;
    send = vi.fn();
    close = vi.fn();

    constructor(url: string) {
        this.url = url;
        FakeWebSocket.instances.push(this);
    }
}

describe('WebSocketService', () => {
    let service: WebSocketService;

    beforeEach(() => {
        FakeWebSocket.instances = [];
        vi.stubGlobal('WebSocket', FakeWebSocket);
        service = new WebSocketService();
    });

    it('connect creates a new coket and calls onOpen', () => {
        const onOpen = vi.fn();

        service.connect('ws://test', { onOpen });

        const instance = FakeWebSocket.instances[0];

        expect(instance).not.toBeNullable();

        expect(instance?.url).toBe('ws://test');

        instance?.onopen?.(new Event('open'));
        expect(onOpen).toHaveBeenCalledTimes(1);
    });

    it('on message when receives an event', () => {
        const onMessage = vi.fn();
        service.connect('ws://test', { onMessage });

        const instance = FakeWebSocket.instances[0];
        expect(instance).not.toBeNullable();
        
        const event = new MessageEvent('message', { data: 'data' });
        instance?.onmessage?.(event);

        expect(onMessage).toHaveBeenCalledWith(event);
    });

    it('send message when connected', () => {
        service.connect('ws://test');
        const instance = FakeWebSocket.instances[0];

        service.send('hello');

        expect(instance?.send).toHaveBeenCalledWith('hello');
    });

    it('send error when disconnected', () => {
        expect(() => service.send('hello')).toThrow('WebSocket is not connected');
    });

    it('close the socket', () => {
        service.connect('ws://test');
        const instance = FakeWebSocket.instances[0];

        service.close();

        expect(instance?.close).toHaveBeenCalledTimes(1);
    });
});