import { QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactElement } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { StudentIdProvider } from '../hooks/StudentIdProvider'
import { createQueryClient } from '../queryClient'

/** Renders a component with the same providers the real app uses (fresh query cache each time). */
export function renderApp(ui: ReactElement, { route = '/' }: { route?: string } = {}) {
  const queryClient = createQueryClient()
  queryClient.setDefaultOptions({
    queries: { retry: false },
    mutations: { retry: false },
  })

  const result = render(
    <QueryClientProvider client={queryClient}>
      <StudentIdProvider>
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      </StudentIdProvider>
    </QueryClientProvider>,
  )
  return { ...result, queryClient }
}
