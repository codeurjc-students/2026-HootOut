import { get } from "@/services/common/api/ApiService";
import type { ChatMessage, PageResult } from "@/types";


const ChatMessageService = {
    getMessagesByChannelId: async function (channelId: string, next: string | undefined = undefined, pageSize: number = 20): Promise<PageResult<ChatMessage>> {
        return get('chatMessages/channelMessages', { channelId, next, pageSize });
    },
}

export default ChatMessageService;