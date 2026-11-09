import { describe, expect, it } from 'vitest'
import { isValidStudentId, normalizeStudentInput } from './studentId'

describe('normalizeStudentInput', () => {
  it('keeps ASCII digits', () => {
    expect(normalizeStudentInput('20152691234567890')).toBe('20152691234567890')
  })

  it('converts Bangla digits to ASCII', () => {
    expect(normalizeStudentInput('২০১৫২৬৯১২৩৪৫৬৭৮৯০')).toBe('20152691234567890')
  })

  it('drops spaces, dashes and letters', () => {
    expect(normalizeStudentInput(' 2015-2691 abc 234 ')).toBe('20152691234')
  })

  it('caps the length at 17 digits', () => {
    expect(normalizeStudentInput('123456789012345678901')).toHaveLength(17)
  })
})

describe('isValidStudentId', () => {
  it('requires exactly 17 digits', () => {
    expect(isValidStudentId('20152691234567890')).toBe(true)
    expect(isValidStudentId('2015269123456789')).toBe(false)
    expect(isValidStudentId('')).toBe(false)
  })
})
