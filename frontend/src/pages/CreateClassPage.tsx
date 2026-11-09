import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { createClass } from '../api/endpoints'
import { ErrorMessage } from '../components/ErrorMessage'
import { MAX_CLASS_NAME_LENGTH, MAX_SEATS, MIN_SEATS, validateClassForm } from './classForm'

export function CreateClassPage() {
  const queryClient = useQueryClient()
  const [name, setName] = useState('')
  const [seats, setSeats] = useState('')
  const [submitted, setSubmitted] = useState(false)
  const [createdName, setCreatedName] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: (values: { name: string; totalSeats: number }) => createClass(values.name, values.totalSeats),
    onSuccess: (created) => {
      setCreatedName(created.name)
      setName('')
      setSeats('')
      setSubmitted(false)
      void queryClient.invalidateQueries({ queryKey: ['classes'] })
    },
  })

  const { values, errors } = validateClassForm(name, seats)
  const showErrors = submitted

  function onSubmit(event: FormEvent) {
    event.preventDefault()
    if (mutation.isPending) return
    setSubmitted(true)
    setCreatedName(null)
    if (!values) return
    mutation.mutate(values)
  }

  return (
    <section className="card">
      <h1>Class তৈরি</h1>
      <p className="muted">
        প্রতিটি shift আলাদা class হিসেবে তৈরি করুন, যেমন: Class 6 - Morning Shift এবং Class 6 - Day Shift।
      </p>

      <form onSubmit={onSubmit} noValidate>
        <div className="field">
          <label htmlFor="class-name">Class-এর নাম</label>
          <input
            id="class-name"
            type="text"
            value={name}
            maxLength={MAX_CLASS_NAME_LENGTH + 20}
            onChange={(event) => setName(event.target.value)}
            aria-invalid={showErrors && !!errors.name}
            aria-describedby={showErrors && errors.name ? 'class-name-error' : undefined}
            placeholder="Class 6 - Morning Shift"
          />
          {showErrors && errors.name && (
            <p id="class-name-error" className="field__error">
              {errors.name}
            </p>
          )}
        </div>

        <div className="field">
          <label htmlFor="class-seats">মোট seat</label>
          <input
            id="class-seats"
            type="number"
            inputMode="numeric"
            min={MIN_SEATS}
            max={MAX_SEATS}
            value={seats}
            onChange={(event) => setSeats(event.target.value)}
            aria-invalid={showErrors && !!errors.totalSeats}
            aria-describedby={showErrors && errors.totalSeats ? 'class-seats-error' : 'class-seats-hint'}
            placeholder="120"
          />
          {showErrors && errors.totalSeats ? (
            <p id="class-seats-error" className="field__error">
              {errors.totalSeats}
            </p>
          ) : (
            <p id="class-seats-hint" className="field__hint">
              {MIN_SEATS} থেকে {MAX_SEATS}-এর মধ্যে একটি পূর্ণ সংখ্যা।
            </p>
          )}
        </div>

        <ErrorMessage error={mutation.error} />

        {createdName && (
          <p className="alert alert--success" role="status">
            “{createdName}” তৈরি হয়েছে। <Link to="/">Class তালিকা দেখুন</Link>
          </p>
        )}

        <div className="actions">
          <button type="submit" className="btn" disabled={mutation.isPending}>
            {mutation.isPending ? 'তৈরি হচ্ছে…' : 'Class তৈরি করুন'}
          </button>
        </div>
      </form>
    </section>
  )
}
