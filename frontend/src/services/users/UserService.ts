import type { Picture, UpdateProfilePictureRequest } from '@/types'
import { put } from '../common/api/ApiService'

const UserService = {
  updateProfilePicture: async function (request: UpdateProfilePictureRequest): Promise<Picture> {
    return await put('users/profile/picture', request)
  },
}

export default UserService
