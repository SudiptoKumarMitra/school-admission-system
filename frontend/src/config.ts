declare global {
  interface Window {
    __APP_CONFIG__?: { API_BASE_URL?: string }
  }
}

const DEFAULT_API_BASE_URL = 'http://localhost:8080'

/**
 * Backend URL, in order of precedence: runtime config (/config.js, written from the API_BASE_URL
 * environment variable by the container), the Vite build/dev variable, then the default.
 */
export function resolveApiBaseUrl(
  runtime: string | undefined = window.__APP_CONFIG__?.API_BASE_URL,
  build: string | undefined = import.meta.env.VITE_API_BASE_URL,
): string {
  const chosen = runtime?.trim() || build?.trim() || DEFAULT_API_BASE_URL
  return chosen.replace(/\/+$/, '')
}

export const apiBaseUrl = resolveApiBaseUrl()
