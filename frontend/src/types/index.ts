export * from './userInfo'
export * from './auth'
export * from './websocketMessage'
export * from './chatMessage'
export * from './chatChannel'

export class ApiError extends Error {
    constructor(public status: number, message: string) {
        super(message);
    }
}