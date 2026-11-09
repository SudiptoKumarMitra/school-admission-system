export const STUDENT_ID_LENGTH = 17
const STORAGE_KEY = 'studentId'
const BANGLA_DIGITS = '০১২৩৪৫৬৭৮৯'

/** Converts Bangla digits to ASCII, drops everything that is not a digit, and caps the length. */
export function normalizeStudentInput(raw: string): string {
  const ascii = Array.from(raw)
    .map((ch) => {
      const index = BANGLA_DIGITS.indexOf(ch)
      return index >= 0 ? String(index) : ch
    })
    .join('')
  return ascii.replace(/\D/g, '').slice(0, STUDENT_ID_LENGTH)
}

export const isValidStudentId = (value: string) => /^\d{17}$/.test(value)

// Storage can be unavailable (private mode, blocked cookies): the app must still work without it.
export function readStoredStudentId(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) ?? ''
  } catch {
    return ''
  }
}

export function writeStoredStudentId(value: string | null) {
  try {
    if (value) localStorage.setItem(STORAGE_KEY, value)
    else localStorage.removeItem(STORAGE_KEY)
  } catch {
    // ignore: the identity just will not survive a reload
  }
}
