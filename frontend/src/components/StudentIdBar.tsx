import { isValidStudentId, STUDENT_ID_LENGTH } from '../hooks/studentId'
import { useStudentId } from '../hooks/useStudentId'

/** The box at the top of every page: the student's birth registration number is their identity. */
export function StudentIdBar() {
  const { input, setInput, clear } = useStudentId()
  const valid = isValidStudentId(input)
  const incomplete = input.length > 0 && !valid

  return (
    <section className="student-bar" aria-label="শিক্ষার্থীর পরিচয়">
      <div className="container student-bar__inner">
        <label htmlFor="student-id" className="student-bar__label">
          শিক্ষার্থীর জন্ম নিবন্ধন নম্বর ({STUDENT_ID_LENGTH} অঙ্ক)
        </label>
        <div className="student-bar__field">
          <input
            id="student-id"
            type="text"
            inputMode="numeric"
            autoComplete="off"
            placeholder="যেমন 20152691234567890"
            value={input}
            onChange={(event) => setInput(event.target.value)}
            aria-invalid={incomplete}
            aria-describedby="student-id-hint"
          />
          {input && (
            <button type="button" className="btn btn--ghost" onClick={clear}>
              মুছুন
            </button>
          )}
        </div>
        <p
          id="student-id-hint"
          className={`student-bar__hint${incomplete ? ' student-bar__hint--error' : ''}${valid ? ' student-bar__hint--ok' : ''}`}
        >
          {valid && '✓ নম্বর সঠিক। এই নম্বর দিয়েই আবেদন করা হবে।'}
          {incomplete && `${STUDENT_ID_LENGTH} অঙ্কের নম্বর দিন (এখন ${input.length} অঙ্ক দেওয়া হয়েছে)।`}
          {!input && 'আবেদন করতে বা আপনার আবেদন দেখতে জন্ম নিবন্ধন নম্বর দিন।'}
        </p>
      </div>
    </section>
  )
}
