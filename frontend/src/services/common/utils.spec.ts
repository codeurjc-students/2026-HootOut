import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { newId, getValidationErrors, dateTimeMapper } from '@/services/common/utils'
import { ValidationError, type ValidationErrorEntry } from '@/types'

const UUID_FORMAT = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/

describe('newId', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  describe('with crypto.randomUUID available', () => {
    it('returns a UUID', () => {
      expect(newId()).toMatch(UUID_FORMAT)
    })

    it('returns the value of crypto.randomUUID', () => {
      const randomUUID = vi.fn(() => '11111111-2222-3333-4444-555555555555')
      vi.stubGlobal('crypto', { randomUUID })

      expect(newId()).toBe('11111111-2222-3333-4444-555555555555')
      expect(randomUUID).toHaveBeenCalledTimes(1)
    })

    it('returns a different id on each call', () => {
      const ids = new Set(Array.from({ length: 1000 }, () => newId()))

      expect(ids.size).toBe(1000)
    })
  })

  describe('without crypto.randomUUID', () => {
    it.each([
      ['crypto is not defined', undefined],
      ['crypto has no randomUUID (non secure context)', {}],
    ])('builds the id from the time and a random part when %s', (_, cryptoValue) => {
      vi.stubGlobal('crypto', cryptoValue)
      vi.useFakeTimers({ toFake: ['Date'] })
      vi.setSystemTime(new Date('2026-11-05T18:30:00Z'))
      vi.spyOn(Math, 'random').mockReturnValue(0.123456789)

      // base 36 timestamp + '-' + first 8 base 36 digits of the random number
      expect(newId()).toBe('mw3vrm80-4fzzzxjy')
    })

    it('returns a different id on each call within the same millisecond', () => {
      vi.stubGlobal('crypto', undefined)
      vi.useFakeTimers({ toFake: ['Date'] })
      vi.setSystemTime(new Date('2026-11-05T18:30:00Z'))

      const ids = new Set(Array.from({ length: 1000 }, () => newId()))

      expect(ids.size).toBe(1000)
    })
  })
})

describe('getValidationErrors', () => {
  const validationError = (errors: ValidationErrorEntry[]) => new ValidationError(errors, 400)

  it('returns an empty map when there are no errors', () => {
    expect(getValidationErrors(validationError([])).size).toBe(0)
  })

  it('maps each property to its error codes and messages', () => {
    const result = getValidationErrors(
      validationError([
        { propertyName: 'username', errorCode: 'NotEmpty', errorMessage: 'Username is required' },
      ])
    )

    expect(result).toEqual(new Map([['username', new Map([['NotEmpty', 'Username is required']])]]))
  })

  it('groups several errors of the same property', () => {
    const result = getValidationErrors(
      validationError([
        { propertyName: 'password', errorCode: 'MinLength', errorMessage: 'Too short' },
        {
          propertyName: 'password',
          errorCode: 'Uppercase',
          errorMessage: 'Needs an uppercase letter',
        },
      ])
    )

    expect(result.size).toBe(1)
    expect(result.get('password')).toEqual(
      new Map([
        ['MinLength', 'Too short'],
        ['Uppercase', 'Needs an uppercase letter'],
      ])
    )
  })

  it('keeps the errors of each property separate', () => {
    const result = getValidationErrors(
      validationError([
        { propertyName: 'username', errorCode: 'NotEmpty', errorMessage: 'Username is required' },
        { propertyName: 'password', errorCode: 'MinLength', errorMessage: 'Too short' },
        { propertyName: 'username', errorCode: 'Unique', errorMessage: 'Username already taken' },
      ])
    )

    expect([...result.keys()]).toEqual(['username', 'password'])
    expect(result.get('username')).toEqual(
      new Map([
        ['NotEmpty', 'Username is required'],
        ['Unique', 'Username already taken'],
      ])
    )
    expect(result.get('password')).toEqual(new Map([['MinLength', 'Too short']]))
  })

  it('keeps the last message when the same property has the same code twice', () => {
    const result = getValidationErrors(
      validationError([
        { propertyName: 'email', errorCode: 'Invalid', errorMessage: 'First message' },
        { propertyName: 'email', errorCode: 'Invalid', errorMessage: 'Second message' },
      ])
    )

    expect(result.get('email')).toEqual(new Map([['Invalid', 'Second message']]))
  })

  it('groups the errors without property name under an undefined key', () => {
    const result = getValidationErrors(
      validationError([{ errorCode: 'General', errorMessage: 'Something went wrong' }])
    )

    expect(result.get(undefined as unknown as string)).toEqual(
      new Map([['General', 'Something went wrong']])
    )
  })
})

describe('dateTimeMapper', () => {
  // The output depends on the time zone of the machine: fix it so the tests
  // give the same result everywhere. Node applies TZ changes at runtime.
  beforeEach(() => {
    vi.stubEnv('TZ', 'Europe/Madrid')
  })

  afterEach(() => {
    vi.unstubAllEnvs()
  })

  // Some ICU versions use a narrow no-break space before AM/PM
  const normalizeSpaces = (value: string) => value.replace(/\s/g, ' ')

  describe('formatting', () => {
    it('formats a UTC date as a short date and time in the given locale', () => {
      expect(dateTimeMapper('2026-11-05T18:30:00Z', 'es-ES')).toBe('5/11/26, 19:30')
    })

    it('uses the format of each locale', () => {
      expect(normalizeSpaces(dateTimeMapper('2026-11-05T18:30:00Z', 'en-US'))).toBe(
        '11/5/26, 7:30 PM'
      )
    })

    it('uses the default locale of the browser when no locale is given', () => {
      const expected = new Intl.DateTimeFormat(undefined, {
        dateStyle: 'short',
        timeStyle: 'short',
      }).format(new Date('2026-11-05T18:30:00Z'))

      expect(dateTimeMapper('2026-11-05T18:30:00Z')).toBe(expected)
    })
  })

  describe('time zones', () => {
    it('converts the date to the local time zone', () => {
      vi.stubEnv('TZ', 'America/New_York')

      expect(dateTimeMapper('2026-11-05T18:30:00Z', 'es-ES')).toBe('5/11/26, 13:30')
    })

    it('changes the day when the local time is past midnight', () => {
      expect(dateTimeMapper('2026-11-05T23:30:00Z', 'es-ES')).toBe('6/11/26, 0:30')
    })

    it('applies daylight saving time', () => {
      expect(dateTimeMapper('2026-07-05T18:30:00Z', 'es-ES')).toBe('5/7/26, 20:30')
    })

    it.each(['2026-11-05T18:30:00', '2026-11-05T18:30', '2026-11-05T18:30:00.123'])(
      'treats a date without time zone (%s) as UTC',
      (dateString) => {
        expect(dateTimeMapper(dateString, 'es-ES')).toBe('5/11/26, 19:30')
      }
    )

    it('respects an explicit offset', () => {
      expect(dateTimeMapper('2026-11-05T18:30:00+02:00', 'es-ES')).toBe('5/11/26, 17:30')
    })
  })

  describe('invalid values', () => {
    it.each([null, undefined, ''])('returns an empty string for %s', (value) => {
      expect(dateTimeMapper(value, 'es-ES')).toBe('')
    })

    it.each(['abc', 'not a date', '2026-13-45T99:99:00Z'])(
      'returns the original value for "%s"',
      (value) => {
        expect(dateTimeMapper(value, 'es-ES')).toBe(value)
      }
    )
  })
})
