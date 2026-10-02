import type { UserInfo } from "./userInfo";

export interface ChatMessage {
  id: string;
  author: UserInfo;
  content: string;
  createdAt: string;
  modifiedAt: string;
}