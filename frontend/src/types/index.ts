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
  next?: string | null
  pageSize: number
  total: number
}

export interface PageRequest extends Record<string, unknown> {
  next?: string | null
  descending: boolean
  sortBy?: string | null
  pageSize?: number | null
}

export interface TableHeader {
  key: string
  name: string
  canSort?: boolean | undefined
  default?: boolean | undefined
  mapper?: (value: any) => string | null | undefined
}
