import { useQuery } from '@tanstack/react-query'
import { ApiError } from '../api/client'
import { getHold, listClasses, listMyHolds } from '../api/endpoints'

/** How often seat counts are refreshed without a page reload (R-UI-3). */
export const CLASS_POLL_MS = 3000
export const MY_HOLDS_POLL_MS = 5000

export function useClasses() {
  return useQuery({
    queryKey: ['classes'],
    queryFn: ({ signal }) => listClasses(signal),
    refetchInterval: CLASS_POLL_MS,
  })
}

/** The current student's applications; disabled until a complete student ID has been entered. */
export function useMyHolds(studentId: string | null) {
  return useQuery({
    queryKey: ['myHolds', studentId],
    queryFn: ({ signal }) => listMyHolds(studentId!, signal),
    enabled: studentId !== null,
    refetchInterval: MY_HOLDS_POLL_MS,
  })
}

export const HOLD_POLL_MS = 5000

/** One application. Polls while it is still HELD; a final status does not change any more. */
export function useHold(holdId: string | undefined) {
  return useQuery({
    queryKey: ['hold', holdId],
    queryFn: ({ signal }) => getHold(holdId!, signal),
    enabled: !!holdId,
    refetchInterval: (query) => (query.state.data?.status === 'HELD' ? HOLD_POLL_MS : false),
    // An answer such as "not found" will not change by asking again.
    retry: (count, error) => !(error instanceof ApiError) && count < 1,
  })
}
