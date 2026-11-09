import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import App from './App.tsx'
import { ErrorBoundary } from './components/ErrorBoundary.tsx'
import { StudentIdProvider } from './hooks/StudentIdProvider.tsx'
import { createQueryClient } from './queryClient.ts'
import './styles.css'

const queryClient = createQueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ErrorBoundary>
      <QueryClientProvider client={queryClient}>
        <StudentIdProvider>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </StudentIdProvider>
      </QueryClientProvider>
    </ErrorBoundary>
  </StrictMode>,
)
