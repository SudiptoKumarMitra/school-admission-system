import { describe, expect, it } from 'vitest'
import { validateClassForm } from './classForm'

describe('validateClassForm', () => {
  it('accepts a valid form and trims the name', () => {
    const { values, errors } = validateClassForm('  Class 6 - Morning Shift ', '120')

    expect(errors).toEqual({})
    expect(values).toEqual({ name: 'Class 6 - Morning Shift', totalSeats: 120 })
  })

  it('accepts the boundaries', () => {
    expect(validateClassForm('A', '1').values).not.toBeNull()
    expect(validateClassForm('x'.repeat(100), '100000').values).not.toBeNull()
  })

  it('requires a name and a seat count', () => {
    const { values, errors } = validateClassForm('   ', '')

    expect(values).toBeNull()
    expect(errors.name).toBeDefined()
    expect(errors.totalSeats).toBeDefined()
  })

  it('rejects a name longer than 100 characters', () => {
    expect(validateClassForm('x'.repeat(101), '5').errors.name).toBeDefined()
  })

  it.each(['0', '-3', '100001', '12.5', '1e3', 'abc', '5 6'])('rejects %s seats', (seats) => {
    expect(validateClassForm('Class', seats).errors.totalSeats).toBeDefined()
  })
})
