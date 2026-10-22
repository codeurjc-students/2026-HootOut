import wsMessageService from "@/services/common/websocketsMesage/WebSocketsMessageService";
import { defineStore } from "pinia";
import { ref, watch } from "vue";

export const wsStore = defineStore('wsStore', () => {
    const wsStatus = ref<number>(WebSocket.CLOSED);
    const wsShouldConnect = ref<boolean>(false);
    const recconnectInterval = 3000;

    let reconnectTimer: number | undefined = undefined;

    watch(wsStatus, async (newStatus) => {
        if (newStatus == WebSocket.CLOSED) {
            if (!wsShouldConnect) {
                if (reconnectTimer) {
                    clearInterval(reconnectTimer);
                    reconnectTimer = undefined;
                }
                return;
            }

            if (!reconnectTimer) {
                reconnectTimer = setInterval(async () => {
                    await connect(async () => { });
                }, recconnectInterval);
            }
        }
    });

    async function connect(callback: () => Promise<void>) {
        await wsMessageService.connect(callback);
    }

    return { wsStatus }
})