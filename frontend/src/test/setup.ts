import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { resetServerReachable } from '../api/connectionStatus'
import { server } from './server'

beforeAll(() => server.listen({ onUnhandledFrame: 'error' }))
afterEach(() => {
  cleanup()
  localStorage.clear()
  resetServerReachable()
  server.resetHandlers()
})
afterAll(() => server.close())
