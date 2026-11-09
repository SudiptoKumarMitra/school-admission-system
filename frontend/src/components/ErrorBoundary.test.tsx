import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useState } from 'react'
import { describe, expect, it, vi } from 'vitest'
import { ErrorBoundary } from './ErrorBoundary'

function Bomb({ explode }: { explode: boolean }) {
  if (explode) throw new Error('render failure')
  return <p>all good</p>
}

describe('ErrorBoundary', () => {
  it('renders its children normally', () => {
    render(
      <ErrorBoundary>
        <Bomb explode={false} />
      </ErrorBoundary>,
    )

    expect(screen.getByText('all good')).toBeInTheDocument()
  })

  it('shows a friendly message instead of a blank page when rendering fails, and can recover', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => {})

    function Harness() {
      const [explode, setExplode] = useState(true)
      return (
        <>
          <button onClick={() => setExplode(false)}>fix</button>
          <ErrorBoundary>
            <Bomb explode={explode} />
          </ErrorBoundary>
        </>
      )
    }
    render(<Harness />)

    expect(screen.getByRole('alert')).toHaveTextContent('কিছু একটা সমস্যা হয়েছে')

    await userEvent.click(screen.getByText('fix'))
    await userEvent.click(screen.getByRole('button', { name: 'আবার চেষ্টা করুন' }))

    expect(screen.getByText('all good')).toBeInTheDocument()
    vi.restoreAllMocks()
  })
})
