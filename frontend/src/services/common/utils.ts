import type { ValidationError } from '@/types'

export function newId(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID()
  }

  return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`
}

export function getValidationErrors(err: ValidationError): Map<string, Map<string, string>> {
  const result = new Map<string, Map<string, string>>()
  err.errors.forEach((x) => {
    if (!result.has(x.propertyName!)) {
      result.set(x.propertyName!, new Map<string, string>())
    }
    const entry = result.get(x.propertyName!)
    entry?.set(x.errorCode!, x.errorMessage!)
  })
  return result
}

// A date-time without offset, e.g. "2026-11-05T18:30:00" or "2026-11-05T18:30:00.123"
const DATE_TIME_WITHOUT_OFFSET = /T\d{2}:\d{2}(:\d{2}(\.\d+)?)?$/

/**
 * Formats a UTC date as a short date and time in the user's time zone.
 * Uses the browser locale unless one is given.
 * Returns the original value when it is not a valid date ('' for null or undefined).
 */
export function dateTimeMapper(date_string: string | null | undefined, locale?: string): string {
  if (!date_string) return ''

  // Dates come in UTC: without an explicit offset, new Date() would read them as local time
  const utcDateString = DATE_TIME_WITHOUT_OFFSET.test(date_string) ? `${date_string}Z` : date_string
  const date = new Date(utcDateString)
  if (Number.isNaN(date.getTime())) return date_string

  return new Intl.DateTimeFormat(locale, {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(date)
}
