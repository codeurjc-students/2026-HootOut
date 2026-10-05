import { get } from "@/services/common/api/ApiService";


const ChatMessageService = {
    getMessagesByChannelId: async function (channelId: string): Promise<Array<ChatMessageDto>> {
        return get('chatMessages/getByChannelId', { channelId });
    },
}

export default ChatMessageService;