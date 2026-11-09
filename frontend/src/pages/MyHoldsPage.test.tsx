import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../test/server'
import { renderApp } from '../test/utils'
import { MyHoldsPage } from './MyHoldsPage'

const API = 'http://localhost:8080'
const STUDENT = '20152691234567890'
const isoIn = (seconds: number) => new Date(Date.now() + seconds * 1000).toISOString()

const hold = (id: string, className: string, status: string, secondsLeft = 300) => ({
  holdId: id,
  classId: `c_${id}`,
  className,
  status,
  createdAt: isoIn(-60),
  confirmedAt: null,
  expiresAt: isoIn(secondsLeft),
  serverTime: new Date().toISOString(),
})

function mockMine(...holds: unknown[]) {
  server.use(http.get(`${API}/me/holds`, () => HttpResponse.json(holds)))
}

const rows = () => screen.getAllByRole('row').slice(1) // without the header row

describe('MyHoldsPage', () => {
  it('asks for the student ID when none is entered', () => {
    renderApp(<MyHoldsPage />)

    expect(screen.getByText('আপনার আবেদনগুলো দেখতে উপরে জন্ম নিবন্ধন নম্বর দিন।')).toBeInTheDocument()
  })

  it('lists every application with its status and a link to its page', async () => {
    localStorage.setItem('studentId', STUDENT)
    let header: string | null = null
    server.use(
      http.get(`${API}/me/holds`, ({ request }) => {
        header = request.headers.get('X-Student-Id')
        return HttpResponse.json([
          hold('h_1', 'Class 6 - Morning Shift', 'CONFIRMED'),
          hold('h_2', 'Class 7 - Day Shift', 'CANCELLED'),
          hold('h_3', 'Class 8 - Morning Shift', 'EXPIRED'),
        ])
      }),
    )
    renderApp(<MyHoldsPage />)

    expect(await screen.findByText('Class 6 - Morning Shift')).toBeInTheDocument()
    expect(header).toBe(STUDENT)
    expect(screen.getByText('CONFIRMED')).toBeInTheDocument()
    expect(screen.getByText('CANCELLED')).toBeInTheDocument()
    expect(screen.getByText('EXPIRED')).toBeInTheDocument()
    expect(within(rows()[0]).getByRole('link', { name: 'দেখুন' })).toHaveAttribute('href', '/holds/h_1')
  })

  it('puts the running application first and highlights it, with its countdown', async () => {
    localStorage.setItem('studentId', STUDENT)
    mockMine(
      hold('h_1', 'Old Class', 'CANCELLED'),
      hold('h_2', 'Class 6 - Morning Shift', 'HELD', 120),
      hold('h_3', 'Another Old Class', 'EXPIRED'),
    )
    renderApp(<MyHoldsPage />)

    await screen.findByText('Class 6 - Morning Shift')

    const first = rows()[0]
    expect(first).toHaveTextContent('Class 6 - Morning Shift')
    expect(first).toHaveClass('my-hold--active')
    expect(first).toHaveTextContent('চলমান আবেদন')
    expect(within(first).getByRole('timer')).toHaveTextContent(/^0[12]:\d\d$/)
    expect(rows()[1]).not.toHaveClass('my-hold--active')
  })

  it('shows EXPIRED by itself when the countdown of a running application ends', async () => {
    localStorage.setItem('studentId', STUDENT)
    mockMine(hold('h_1', 'Class 6 - Morning Shift', 'HELD', 2))
    renderApp(<MyHoldsPage />)

    expect(await screen.findByText('HELD')).toBeInTheDocument()

    expect(await screen.findByText('EXPIRED', {}, { timeout: 5000 })).toBeInTheDocument()
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()
    expect(rows()[0]).not.toHaveClass('my-hold--active')
  }, 15000)

  it('still shows the running application after a reload (remount with the stored ID)', async () => {
    localStorage.setItem('studentId', STUDENT)
    mockMine(hold('h_2', 'Class 6 - Morning Shift', 'HELD', 300))
    const first = renderApp(<MyHoldsPage />)
    await screen.findByText('Class 6 - Morning Shift')
    first.unmount()

    renderApp(<MyHoldsPage />)

    expect(await screen.findByText('Class 6 - Morning Shift')).toBeInTheDocument()
    expect(screen.getByText('HELD')).toBeInTheDocument()
  })

  it('shows an empty state with a link to the class list', async () => {
    localStorage.setItem('studentId', STUDENT)
    mockMine()
    renderApp(<MyHoldsPage />)

    expect(await screen.findByText('এই শিক্ষার্থীর এখনো কোনো আবেদন নেই।')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Class তালিকা দেখুন' })).toHaveAttribute('href', '/')
  })

  it('shows a readable error with a retry button when the server is down', async () => {
    localStorage.setItem('studentId', STUDENT)
    server.use(http.get(`${API}/me/holds`, () => HttpResponse.error()))
    renderApp(<MyHoldsPage />)

    expect(await screen.findByRole('alert')).toHaveTextContent('Server-এর সাথে যোগাযোগ করা যাচ্ছে না')

    mockMine(hold('h_1', 'Class 6 - Morning Shift', 'CONFIRMED'))
    await userEvent.click(screen.getByRole('button', { name: 'আবার চেষ্টা করুন' }))
    expect(await screen.findByText('Class 6 - Morning Shift')).toBeInTheDocument()
  })
})
