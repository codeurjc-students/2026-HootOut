export * from './userInfo'
export * from './auth'
export * from './websocketMessage'
export * from './chatMessage'
export * from './chatChannel'
export * from './chatServer'
export * from './mediaFile'

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string
  ) {
    super(message)
  }
}

export class ValidationError extends Error {
  constructor(
    public errors: ValidationErrorEntry[],
    public status: number,
    message: string = 'Validation Error'
  ) {
    super(message)
  }
}

export interface ValidationErrorEntry {
  propertyName?: string | null
  errorMessage?: string | null
  errorCode?: string | null
  formattedMessagePlaceholderValues?: any
}

export interface PageResult<T> {
  items: T[]
  next?: string | undefined
  pageSize: number
}
