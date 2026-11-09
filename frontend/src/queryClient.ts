import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query'
import { NetworkError } from './api/client'
import { setServerReachable } from './api/connectionStatus'

// Any answer from the server (even an error answer) proves it is reachable; a NetworkError does not.
const reportReachability = (error: unknown) => setServerReachable(!(error instanceof NetworkError))

export function createQueryClient() {
  return new QueryClient({
    queryCache: new QueryCache({
      onSuccess: () => setServerReachable(true),
      onError: reportReachability,
    }),
    mutationCache: new MutationCache({
      onSuccess: () => setServerReachable(true),
      onError: reportReachability,
    }),
    defaultOptions: {
      queries: { retry: 1, refetchOnWindowFocus: true },
      mutations: { retry: false },
    },
  })
}
