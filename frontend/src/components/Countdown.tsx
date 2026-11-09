import { formatRemaining } from '../hooks/countdown'

export function Countdown({ remainingMs }: { remainingMs: number }) {
  const low = remainingMs <= 60_000
  return (
    <span className={`countdown${low ? ' countdown--low' : ''}`} role="timer" aria-live="off">
      {formatRemaining(remainingMs)}
    </span>
  )
}
