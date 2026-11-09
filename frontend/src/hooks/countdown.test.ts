import { describe, expect, it } from 'vitest'
import { computeRemainingMs, formatRemaining } from './countdown'

describe('computeRemainingMs', () => {
  it('counts down to the deadline', () => {
    const t0 = Date.parse('2026-11-01T04:00:00Z')

    const remaining = computeRemainingMs('2026-11-01T04:10:00Z', '2026-11-01T04:00:00Z', t0, t0 + 30_000)

    expect(remaining).toBe(570_000)
  })

  it('corrects for a browser clock that is far from the server clock', () => {
    // The server says it is 04:00:00, the browser thinks it is two hours later.
    const browserNow = Date.parse('2026-11-01T06:00:00Z')

    const remaining = computeRemainingMs('2026-11-01T04:10:00Z', '2026-11-01T04:00:00Z', browserNow, browserNow + 5_000)

    expect(remaining).toBe(595_000)
  })

  it('works without a server time (falls back to the browser clock)', () => {
    const now = Date.parse('2026-11-01T04:00:00Z')

    expect(computeRemainingMs('2026-11-01T04:01:00Z', undefined, now, now)).toBe(60_000)
  })

  it('never goes below zero', () => {
    const t0 = Date.parse('2026-11-01T04:00:00Z')

    expect(computeRemainingMs('2026-11-01T04:00:10Z', '2026-11-01T04:00:00Z', t0, t0 + 60_000)).toBe(0)
  })
})

describe('formatRemaining', () => {
  it.each([
    [600_000, '10:00'],
    [599_000, '09:59'],
    [59_001, '01:00'],
    [1, '00:01'],
    [0, '00:00'],
    [3_723_000, '1:02:03'],
  ])('formats %i ms as %s', (ms, text) => {
    expect(formatRemaining(ms)).toBe(text)
  })
})
