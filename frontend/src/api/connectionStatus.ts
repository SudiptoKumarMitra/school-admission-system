import { useSyncExternalStore } from 'react'

// Tracks whether the last request reached the server, so one banner can warn on every page.
let reachable = true
const listeners = new Set<() => void>()

export function setServerReachable(value: boolean) {
  if (value === reachable) return
  reachable = value
  listeners.forEach((listener) => listener())
}

export function resetServerReachable() {
  setServerReachable(true)
}

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

export function useServerReachable(): boolean {
  return useSyncExternalStore(subscribe, () => reachable)
}
