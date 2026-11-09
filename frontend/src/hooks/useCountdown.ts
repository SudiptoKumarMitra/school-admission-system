import { useEffect, useState } from 'react'
import { computeRemainingMs } from './countdown'

/**
 * Live remaining time in milliseconds (null while there is nothing to count down to). Ticks every
 * second and also fires exactly when the deadline passes, so expiry shows up on time.
 */
export function useCountdown(
  expiresAt: string | undefined,
  serverTime: string | undefined,
  receivedAt: number,
): number | null {
  const [now, setNow] = useState(() => Date.now())

  useEffect(() => {
    if (!expiresAt) return
    // Re-read the clock right away (new data may have arrived), then every second.
    const resync = setTimeout(() => setNow(Date.now()), 0)
    const tick = setInterval(() => setNow(Date.now()), 1000)
    const remaining = computeRemainingMs(expiresAt, serverTime, receivedAt, Date.now())
    const atDeadline = remaining > 0 ? setTimeout(() => setNow(Date.now()), remaining + 20) : undefined
    return () => {
      clearTimeout(resync)
      clearInterval(tick)
      if (atDeadline) clearTimeout(atDeadline)
    }
  }, [expiresAt, serverTime, receivedAt])

  // `now` can be older than freshly arrived data until the next tick; data cannot come from the
  // future, so never use a clock reading earlier than the moment it was received.
  return expiresAt ? computeRemainingMs(expiresAt, serverTime, receivedAt, Math.max(now, receivedAt)) : null
}
