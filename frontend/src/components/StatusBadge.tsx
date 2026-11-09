import type { HoldStatus } from '../api/types'
import { statusLabels } from '../i18n/messages'

/** The status is shown as text (the English code plus a Bangla gloss), never by colour alone. */
export function StatusBadge({ status }: { status: HoldStatus }) {
  return (
    <span className={`badge badge--${status.toLowerCase()}`}>
      <strong>{status}</strong>
      <span className="badge__gloss">{statusLabels[status]}</span>
    </span>
  )
}
