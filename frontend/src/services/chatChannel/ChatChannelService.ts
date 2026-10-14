import type { ChatChannel, CreateChatChannelRequest } from "@/types";
import { get, post } from "../common/api/ApiService"

const ChatChannelService = {
    getAllChannels: async function (): Promise<Array<any>> {
        return get('chatchannels/all');
    },

    getChannelsByServerId: async function (serverId: string): Promise<Array<ChatChannel>> {
        return get('chatchannels/getByServerId', { serverId });
    },

    createNewChannel: async function (request: CreateChatChannelRequest): Promise<ChatChannel> {
        return post('chatchannels', request);
    }
}

export default ChatChannelService;