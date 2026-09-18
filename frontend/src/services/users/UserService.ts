import { get } from "../common/api/ApiService"

const UserService = {
    getAllUsers: async function (): Promise<Array<any>> {
        try {
            return get('user/all');
        } catch (ex) {
            return [];
        }
    }
}

export default UserService;