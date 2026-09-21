import { get } from "../common/api/ApiService"

const UserService = {
    getAllUsers: async function (): Promise<Array<any>> {
        return get('users/all');
    }
}

export default UserService;