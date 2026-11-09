import { describe, expect, it } from 'vitest'
import { ApiError, NetworkError } from '../api/client'
import { errorMessages, messageForError, networkErrorMessage, serverErrorMessage } from './messages'

describe('messageForError', () => {
  it('maps every known API error code to a Bangla message', () => {
    for (const [code, message] of Object.entries(errorMessages)) {
      expect(messageForError(new ApiError(409, code, 'english detail'))).toBe(message)
    }
  })

  it('tells the user the seat is sold out', () => {
    expect(messageForError(new ApiError(409, 'NO_SEATS_AVAILABLE', 'x'))).toBe('এই class-এ আর কোনো খালি seat নেই।')
  })

  it('explains an unreachable server', () => {
    expect(messageForError(new NetworkError())).toBe(networkErrorMessage)
    expect(messageForError(new NetworkError(true))).toBe(networkErrorMessage)
  })

  it('lists which form fields were wrong for a validation error', () => {
    const message = messageForError(
      new ApiError(400, 'VALIDATION_ERROR', 'x', { name: ['a'], totalSeats: ['b'] }),
    )

    expect(message).toContain('তথ্য সঠিক নয়')
    expect(message).toContain('Class-এর নাম')
    expect(message).toContain('মোট seat')
  })

  it('never leaks raw English or technical text for unknown errors', () => {
    expect(messageForError(new ApiError(500, 'INTERNAL_ERROR', 'stack trace'))).toBe(serverErrorMessage)
    expect(messageForError(new Error('boom'))).toBe(serverErrorMessage)
    expect(messageForError('weird')).toBe(serverErrorMessage)
  })
})
