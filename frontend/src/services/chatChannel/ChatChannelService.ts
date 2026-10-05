import type { ChatChannelDto } from "@/types";
import { get, post } from "../common/api/ApiService"

const ChatChannelService = {
    getAllChannels: async function (): Promise<Array<any>> {
        return get('chatchannels/all');
    },

    createNewChannel: async function (name: string): Promise<ChatChannelDto> {
        return post('chatchannels', { name });
    }
}

export default ChatChannelService;