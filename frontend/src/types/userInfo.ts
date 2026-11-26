import type { Picture } from './chatServer'

export interface UserInfo {
  id?: string | null
  email?: string | null
  username?: string
  profilePicture?: Picture | undefined
  createdAt?: string | null
  modifiedAt?: string | null
}

export interface UpdateProfilePictureRequest {
  mediaFileId: string
}
