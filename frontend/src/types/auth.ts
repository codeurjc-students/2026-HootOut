export interface AuthResponse {
    accessToken: string,
    tokenType: string,
    expiresIn?: number
}

export interface WSTicketRespone {
    ticket: string
}