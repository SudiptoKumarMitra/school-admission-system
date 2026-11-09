import { setupServer } from 'msw/node'

/** Mock backend shared by all tests; individual tests add handlers with server.use(...). */
export const server = setupServer()
