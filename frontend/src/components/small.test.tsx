import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ApiError, NetworkError } from '../api/client'
import type { HoldStatus } from '../api/types'
import { Countdown } from './Countdown'
import { ErrorMessage } from './ErrorMessage'
import { StatusBadge } from './StatusBadge'

describe('StatusBadge', () => {
  it.each<[HoldStatus, string]>([
    ['HELD', 'seat hold-এ আছে'],
    ['CONFIRMED', 'ভর্তি নিশ্চিত'],
    ['EXPIRED', 'মেয়াদ শেষ'],
    ['CANCELLED', 'বাতিল করা হয়েছে'],
  ])('shows the code and a Bangla gloss for %s (not colour alone)', (status, gloss) => {
    render(<StatusBadge status={status} />)

    expect(screen.getByText(status)).toBeInTheDocument()
    expect(screen.getByText(gloss)).toBeInTheDocument()
  })
})

describe('Countdown', () => {
  it('formats the time left as mm:ss', () => {
    render(<Countdown remainingMs={125_000} />)

    expect(screen.getByRole('timer')).toHaveTextContent('02:05')
    expect(screen.getByRole('timer')).not.toHaveClass('countdown--low')
  })

  it('is marked as urgent in the last minute', () => {
    render(<Countdown remainingMs={30_000} />)

    expect(screen.getByRole('timer')).toHaveClass('countdown--low')
  })
})

describe('ErrorMessage', () => {
  it('renders nothing when there is no error', () => {
    const { container } = render(<ErrorMessage error={null} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('shows API errors in Bangla', () => {
    render(<ErrorMessage error={new ApiError(409, 'HOLD_EXPIRED', 'The application has expired.')} />)

    expect(screen.getByRole('alert')).toHaveTextContent('আবেদনের মেয়াদ শেষ হয়ে গেছে')
    expect(screen.getByRole('alert')).not.toHaveTextContent('The application has expired')
  })

  it('shows an unreachable server in plain language', () => {
    render(<ErrorMessage error={new NetworkError()} />)

    expect(screen.getByRole('alert')).toHaveTextContent('Server-এর সাথে যোগাযোগ করা যাচ্ছে না')
  })
})
