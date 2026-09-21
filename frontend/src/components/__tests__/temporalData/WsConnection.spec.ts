import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import { nextTick } from 'vue';
import WsConnection from '../../temporalData/WsConnection.vue';
import websocketsService from '@/services/common/websockets/WebSocketService';

vi.mock('@/services/common/websockets/WebSocketService', () => ({
    default: {
        connect: vi.fn(),
        send: vi.fn(),
        close: vi.fn(),
    },
}));

function getHandlers() {
    const call = (websocketsService.connect as ReturnType<typeof vi.fn>).mock.calls[0];
    return call[1];
}

describe('WsConnection.vue', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('Call to connect on mount', () => {
        mount(WsConnection);
        expect(websocketsService.connect).toHaveBeenCalledTimes(1);
        expect(websocketsService.connect).toHaveBeenCalledWith(
            expect.any(String),
            expect.objectContaining({
                onOpen: expect.any(Function),
                onClose: expect.any(Function),
                onError: expect.any(Function),
                onMessage: expect.any(Function),
            })
        );
    });

    it('shows Connection Opened after onOpen', async () => {
        const wrapper = mount(WsConnection);

        getHandlers().onOpen();
        await nextTick();

        expect(wrapper.text()).toContain('Connection Opened');
    });

    it('Shows connection closed after onClose', async () => {
        const wrapper = mount(WsConnection);

        getHandlers().onClose();
        await nextTick();

        expect(wrapper.text()).toContain('Connection Closed');
    });

    it('Shows connection error after onError', async () => {
        const wrapper = mount(WsConnection);

        getHandlers().onError();
        await nextTick();

        expect(wrapper.text()).toContain('Connection Error');
    });

    it('Shows message after onMessage', async () => {
        const wrapper = mount(WsConnection);

        getHandlers().onMessage({ data: 'hello from server' } as MessageEvent);
        await nextTick();

        expect(wrapper.text()).toContain('hello from server');
    });

    it('Input text is sent after send button is clicked', async () => {
        const wrapper = mount(WsConnection);

        await wrapper.find('input').setValue('test message 123');
        await wrapper.find('button').trigger('click');

        expect(websocketsService.send).toHaveBeenCalledWith('test message 123');
    });

    it('websocket close on unmount', () => {
        const wrapper = mount(WsConnection);
        wrapper.unmount();

        expect(websocketsService.close).toHaveBeenCalledTimes(1);
    });
});