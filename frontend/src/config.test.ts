import { describe, expect, it } from 'vitest'
import { resolveApiBaseUrl } from './config'

describe('resolveApiBaseUrl', () => {
  it('prefers the runtime config over the build variable and the default', () => {
    expect(resolveApiBaseUrl('http://api.runtime', 'http://api.build')).toBe('http://api.runtime')
  })

  it('falls back to the build variable, then to localhost:8080', () => {
    expect(resolveApiBaseUrl(undefined, 'http://api.build')).toBe('http://api.build')
    expect(resolveApiBaseUrl('', undefined)).toBe('http://localhost:8080')
    expect(resolveApiBaseUrl('   ', '   ')).toBe('http://localhost:8080')
  })

  it('removes trailing slashes so paths can be appended', () => {
    expect(resolveApiBaseUrl('http://api.runtime///', undefined)).toBe('http://api.runtime')
  })
})
