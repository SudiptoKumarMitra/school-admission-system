/**
 * Milliseconds left until `expiresAt`, judged on the server's clock.
 *
 * `serverTime` is the server's "now" when the data was fetched and `receivedAt` is the browser's
 * clock at that moment, so `serverTime - receivedAt` is how far the browser clock is off. Using it
 * keeps the countdown right even if the user's computer clock is wrong.
 */
export function computeRemainingMs(
  expiresAt: string,
  serverTime: string | undefined,
  receivedAt: number,
  now: number,
): number {
  const offset = serverTime ? Date.parse(serverTime) - receivedAt : 0
  return Math.max(0, Date.parse(expiresAt) - (now + offset))
}

/** 09:05 for minutes and seconds; 1:02:03 once an hour or more is left. */
export function formatRemaining(ms: number): string {
  const total = Math.ceil(ms / 1000)
  const hours = Math.floor(total / 3600)
  const minutes = Math.floor((total % 3600) / 60)
  const seconds = total % 60
  const mm = String(minutes).padStart(2, '0')
  const ss = String(seconds).padStart(2, '0')
  return hours > 0 ? `${hours}:${mm}:${ss}` : `${mm}:${ss}`
}
