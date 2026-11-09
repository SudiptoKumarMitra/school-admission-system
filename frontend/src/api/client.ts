import { apiBaseUrl } from '../config'

const REQUEST_TIMEOUT_MS = 10_000

/** The server answered with an error (problem+json with a machine-readable `code`). */
export class ApiError extends Error {
  status: number
  code: string
  fieldErrors?: Record<string, string[]>

  constructor(status: number, code: string, message: string, fieldErrors?: Record<string, string[]>) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.fieldErrors = fieldErrors
  }
}

/** The server could not be reached, or did not answer in time. */
export class NetworkError extends Error {
  timedOut: boolean

  constructor(timedOut = false) {
    super(timedOut ? 'The request timed out' : 'The server could not be reached')
    this.name = 'NetworkError'
    this.timedOut = timedOut
  }
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'DELETE'
  body?: unknown
  studentId?: string
  signal?: AbortSignal
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, studentId, signal } = options

  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (studentId) headers['X-Student-Id'] = studentId

  const timeout = new AbortController()
  const timer = setTimeout(() => timeout.abort(), REQUEST_TIMEOUT_MS)
  const onCallerAbort = () => timeout.abort()
  signal?.addEventListener('abort', onCallerAbort)

  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: timeout.signal,
    })
  } catch (error) {
    if (signal?.aborted) throw error // cancelled by the caller (for example a query being replaced)
    throw new NetworkError(timeout.signal.aborted)
  } finally {
    clearTimeout(timer)
    signal?.removeEventListener('abort', onCallerAbort)
  }

  if (response.status === 204) return undefined as T

  const text = await response.text()
  let data: unknown
  try {
    data = text ? JSON.parse(text) : undefined
  } catch {
    data = undefined
  }

  if (!response.ok) {
    const problem = (data ?? {}) as { code?: string; detail?: string; title?: string; errors?: Record<string, string[]> }
    throw new ApiError(
      response.status,
      problem.code ?? `HTTP_${response.status}`,
      problem.detail ?? problem.title ?? response.statusText,
      problem.errors,
    )
  }

  return data as T
}
