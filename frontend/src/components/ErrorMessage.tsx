import { messageForError } from '../i18n/messages'

/** Shows a thrown error as a readable message next to the action that failed. */
export function ErrorMessage({ error }: { error: unknown }) {
  if (!error) return null
  return (
    <p className="alert alert--error" role="alert">
      {messageForError(error)}
    </p>
  )
}
