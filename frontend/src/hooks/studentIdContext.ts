import { createContext } from 'react'

export interface StudentIdContextValue {
  /** What is currently typed in the box (digits only). */
  input: string
  setInput: (raw: string) => void
  /** The identity to send to the API: the 17-digit number, or null while it is missing or incomplete. */
  studentId: string | null
  clear: () => void
}

export const StudentIdContext = createContext<StudentIdContextValue | null>(null)
