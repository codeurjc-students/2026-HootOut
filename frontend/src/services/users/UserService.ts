import { get } from '../common/api/ApiService'

const UserService = {
  getAllUsers: async function (): Promise<Array<any>> {
    return await get('users/all')
  },
}

export default UserService
