import { get } from "@/services/common/api/ApiService";
import type { ChatMessage } from "@/types";


const ChatMessageService = {
    getMessagesByChannelId: async function (channelId: string): Promise<Array<ChatMessage>> {
        return get('chatMessages/getByChannelId', { channelId });
    },
}

export default ChatMessageService;