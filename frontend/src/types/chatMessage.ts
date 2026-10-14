import type { UserInfo } from "./userInfo";

export interface ChatMessage {
  id: string;
  author: UserInfo;
  content: string;
  createdAt: string | null;
  modifiedAt: string | null;
}

export interface CreateChatMessage {
  content: string
}