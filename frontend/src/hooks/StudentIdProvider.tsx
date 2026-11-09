import { useCallback, useMemo, useState, type ReactNode } from 'react'
import {
  isValidStudentId,
  normalizeStudentInput,
  readStoredStudentId,
  writeStoredStudentId,
} from './studentId'
import { StudentIdContext, type StudentIdContextValue } from './studentIdContext'

export function StudentIdProvider({ children }: { children: ReactNode }) {
  const [input, setInputState] = useState(() => normalizeStudentInput(readStoredStudentId()))

  const setInput = useCallback((raw: string) => {
    const next = normalizeStudentInput(raw)
    setInputState(next)
    // Only a complete number is remembered, so a reload restores exactly a usable identity.
    writeStoredStudentId(isValidStudentId(next) ? next : null)
  }, [])

  const clear = useCallback(() => setInput(''), [setInput])

  const value = useMemo<StudentIdContextValue>(
    () => ({ input, setInput, clear, studentId: isValidStudentId(input) ? input : null }),
    [input, setInput, clear],
  )

  return <StudentIdContext.Provider value={value}>{children}</StudentIdContext.Provider>
}
