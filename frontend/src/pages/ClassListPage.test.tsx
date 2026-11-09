import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '../test/server'
import { renderApp } from '../test/utils'
import { ClassListPage } from './ClassListPage'

const API = 'http://localhost:8080'
const STUDENT = '20152691234567890'

const morning = { id: 'c_1', name: 'Class 6 - Morning Shift', totalSeats: 120, heldSeats: 15, confirmedSeats: 80, availableSeats: 25 }
const day = { id: 'c_2', name: 'Class 6 - Day Shift', totalSeats: 50, heldSeats: 0, confirmedSeats: 50, availableSeats: 0 }

function renderList() {
  return renderApp(
    <Routes>
      <Route path="/" element={<ClassListPage />} />
      <Route path="/holds/:holdId" element={<p>hold page</p>} />
    </Routes>,
  )
}

const rowOf = (name: string) => screen.getByRole('row', { name: new RegExp(name) })

function mockClasses(...classes: unknown[]) {
  server.use(http.get(`${API}/classes`, () => HttpResponse.json(classes)))
}

describe('ClassListPage', () => {
  beforeEach(() => {
    server.use(http.get(`${API}/me/holds`, () => HttpResponse.json([])))
  })

  it('shows total, held, confirmed and available seats for every class', async () => {
    mockClasses(morning, day)
    renderList()

    const row = within(await screen.findByRole('row', { name: /Morning Shift/ }))
    expect(row.getAllByRole('cell').map((cell) => cell.textContent?.trim().split(' ')[0])).toEqual(
      expect.arrayContaining(['120', '15', '80', '25']),
    )
    expect(screen.getByText(/সর্বশেষ হালনাগাদ/)).toBeInTheDocument()
  })

  it('shows an empty state with a link to create a class', async () => {
    mockClasses()
    renderList()

    expect(await screen.findByText('এখনো কোনো class তৈরি হয়নি।')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Class তৈরি করুন' })).toHaveAttribute('href', '/classes/new')
  })

  it('disables applying until a student ID is entered and says why', async () => {
    mockClasses(morning)
    renderList()

    await screen.findByText('Class 6 - Morning Shift')

    expect(within(rowOf('Morning Shift')).getByRole('button', { name: 'আবেদন করুন' })).toBeDisabled()
    expect(screen.getByText('আবেদন করতে আগে উপরে জন্ম নিবন্ধন নম্বর দিন।')).toBeInTheDocument()
  })

  it('disables applying to a class with no seats left', async () => {
    localStorage.setItem('studentId', STUDENT)
    mockClasses(morning, day)
    renderList()

    await screen.findByText('Class 6 - Day Shift')

    expect(within(rowOf('Day Shift')).getByRole('button', { name: 'আবেদন করুন' })).toBeDisabled()
    expect(within(rowOf('Day Shift')).getByText(/seat শেষ/)).toBeInTheDocument()
    expect(within(rowOf('Morning Shift')).getByRole('button', { name: 'আবেদন করুন' })).toBeEnabled()
  })

  it('applies with the student ID and goes to the new application', async () => {
    localStorage.setItem('studentId', STUDENT)
    let header: string | null = null
    server.use(
      http.post(`${API}/classes/c_1/holds`, ({ request }) => {
        header = request.headers.get('X-Student-Id')
        return HttpResponse.json(
          { holdId: 'h_9', classId: 'c_1', studentId: STUDENT, status: 'HELD', expiresAt: '2030-01-01T00:00:00Z' },
          { status: 201 },
        )
      }),
    )
    mockClasses(morning)
    renderList()

    await userEvent.click(await screen.findByRole('button', { name: 'আবেদন করুন' }))

    expect(await screen.findByText('hold page')).toBeInTheDocument()
    expect(header).toBe(STUDENT)
  })

  it('explains that the seat is gone when applying fails, and refreshes the counts', async () => {
    localStorage.setItem('studentId', STUDENT)
    let listCalls = 0
    server.use(
      http.get(`${API}/classes`, () => (listCalls++, HttpResponse.json([morning]))),
      http.post(`${API}/classes/c_1/holds`, () =>
        HttpResponse.json({ status: 409, code: 'NO_SEATS_AVAILABLE' }, { status: 409 }),
      ),
    )
    renderList()

    await userEvent.click(await screen.findByRole('button', { name: 'আবেদন করুন' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('এই class-এ আর কোনো খালি seat নেই।')
    expect(listCalls).toBeGreaterThanOrEqual(2)
  })

  it('shows a link to the running application instead of an apply button', async () => {
    localStorage.setItem('studentId', STUDENT)
    server.use(
      http.get(`${API}/me/holds`, () =>
        HttpResponse.json([
          { holdId: 'h_7', classId: 'c_1', className: morning.name, status: 'HELD', expiresAt: '2030-01-01T00:00:00Z' },
          { holdId: 'h_8', classId: 'c_2', className: day.name, status: 'CONFIRMED', expiresAt: '2030-01-01T00:00:00Z' },
        ]),
      ),
    )
    mockClasses(morning, { ...day, availableSeats: 3 })
    renderList()

    const link = await screen.findByRole('link', { name: 'আবেদন করা আছে — দেখুন' })
    expect(link).toHaveAttribute('href', '/holds/h_7')
    expect(screen.getByRole('link', { name: 'ভর্তি নিশ্চিত — দেখুন' })).toHaveAttribute('href', '/holds/h_8')
    expect(screen.queryByRole('button', { name: 'আবেদন করুন' })).not.toBeInTheDocument()
  })

  it('does not block applying when the old application expired or was cancelled', async () => {
    localStorage.setItem('studentId', STUDENT)
    server.use(
      http.get(`${API}/me/holds`, () =>
        HttpResponse.json([
          { holdId: 'h_7', classId: 'c_1', className: morning.name, status: 'EXPIRED', expiresAt: '2020-01-01T00:00:00Z' },
          { holdId: 'h_6', classId: 'c_1', className: morning.name, status: 'CANCELLED', expiresAt: '2020-01-01T00:00:00Z' },
        ]),
      ),
    )
    mockClasses(morning)
    renderList()

    expect(await screen.findByRole('button', { name: 'আবেদন করুন' })).toBeEnabled()
  })

  it('refreshes the seat counts by itself, without a page reload', async () => {
    let held = 15
    server.use(
      http.get(`${API}/classes`, () => {
        const response = HttpResponse.json([{ ...morning, heldSeats: held, availableSeats: 120 - 80 - held }])
        held = 16 // the next poll sees one more hold
        return response
      }),
    )
    renderList()

    expect(await within(await screen.findByRole('row', { name: /Morning Shift/ })).findByText('25')).toBeInTheDocument()

    // Nobody touches the page: the next poll (every 3 s) brings the new numbers.
    expect(await within(rowOf('Morning Shift')).findByText('24', {}, { timeout: 6000 })).toBeInTheDocument()
  }, 15000)

  it('shows a readable error with a retry button when the list cannot be loaded', async () => {
    server.use(http.get(`${API}/classes`, () => HttpResponse.error()))
    renderList()

    expect(await screen.findByRole('alert')).toHaveTextContent('Server-এর সাথে যোগাযোগ করা যাচ্ছে না')

    mockClasses(morning)
    await userEvent.click(screen.getByRole('button', { name: 'আবার চেষ্টা করুন' }))
    expect(await screen.findByText('Class 6 - Morning Shift')).toBeInTheDocument()
  })
})
