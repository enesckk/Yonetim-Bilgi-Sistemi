import { describe, expect, it } from 'vitest'
import { telHref } from './phone'

describe('phone dial links', () => {
  it.each([
    ['0532 123 45 67', 'tel:+905321234567'],
    ['(532) 123-45-67', 'tel:+905321234567'],
    ['+90 532 123 45 67', 'tel:+905321234567'],
    ['90 532 123 45 67', 'tel:+905321234567'],
    ['0090 532 123 45 67', 'tel:+905321234567'],
    ['+44 20 7946 0958', 'tel:+442079460958'],
  ])('normalizes %s without changing its country code', (input, expected) => expect(telHref(input)).toBe(expected))
  it.each([null, '', '—', '0532 *** ** 67', '0532 123 45 67 dahili 12', 'javascript:5321234567', '123', '1234567890123456'])('does not dial missing, masked or invalid input %s', input => expect(telHref(input)).toBeNull())
})
