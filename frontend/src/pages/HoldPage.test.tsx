import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '../test/server'
import { renderApp } from '../test/utils'
import { HoldPage } from './HoldPage'

const API = 'http://localhost:8080'
const STUDENT = '20152691234567890'

const isoIn = (seconds: number) => new Date(Date.now() + seconds * 1000).toISOString()

function heldHold(secondsLeft: number, overrides: Record<string, unknown> = {}) {
  return {
    holdId: 'h_1',
    classId: 'c_1',
    className: 'Class 6 - Morning Shift',
    status: 'HELD',
    createdAt: isoIn(-10),
    confirmedAt: null,
    expiresAt: isoIn(secondsLeft),
    serverTime: new Date().toISOString(),
    ...overrides,
  }
}

const mine = (status = 'HELD') => [
  { holdId: 'h_1', classId: 'c_1', className: 'Class 6 - Morning Shift', status, expiresAt: isoIn(60) },
]

function renderHold() {
  return renderApp(
    <Routes>
      <Route path="/holds/:holdId" element={<HoldPage />} />
    </Routes>,
    { route: '/holds/h_1' },
  )
}

const payButton = () => screen.getByRole('button', { name: /ভর্তি ফি দিন|প্রক্রিয়া চলছে/ })

describe('HoldPage', () => {
  beforeEach(() => {
    localStorage.setItem('studentId', STUDENT)
    server.use(http.get(`${API}/me/holds`, () => HttpResponse.json(mine())))
  })

  it('shows the application with its status and the time left', async () => {
    server.use(http.get(`${API}/holds/h_1`, () => HttpResponse.json(heldHold(90))))
    renderHold()

    expect(await screen.findByText('Class 6 - Morning Shift')).toBeInTheDocument()
    expect(screen.getByText('HELD')).toBeInTheDocument()
    expect(screen.getByRole('timer')).toHaveTextContent(/^01:(2[89]|30)$/)
    expect(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' })).toBeEnabled()
  })

  it('counts down from the server clock even when the browser clock is wrong', async () => {
    // The server believes it is 2030-01-01 00:00:00; the browser clock says something else entirely.
    server.use(
      http.get(`${API}/holds/h_1`, () =>
        HttpResponse.json(heldHold(0, { serverTime: '2030-01-01T00:00:00Z', expiresAt: '2030-01-01T00:10:00Z' })),
      ),
    )
    renderHold()

    expect(await screen.findByRole('timer')).toHaveTextContent(/^(09:5\d|10:00)$/)
    expect(screen.queryByText('EXPIRED')).not.toBeInTheDocument()
  })

  it('switches to EXPIRED and disables paying when the time runs out, with no help from the server', async () => {
    let calls = 0
    server.use(
      http.get(`${API}/holds/h_1`, () => {
        calls++
        // The first answer is a hold with 2 seconds left. Any later refetch never answers.
        return calls === 1 ? HttpResponse.json(heldHold(2)) : new Promise<never>(() => {})
      }),
    )
    renderHold()

    expect(await screen.findByText('HELD')).toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' })).toBeEnabled()

    expect(await screen.findByText('EXPIRED', {}, { timeout: 5000 })).toBeInTheDocument()
    expect(payButton()).toBeDisabled()
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()
    expect(screen.getByText(/আবেদনের মেয়াদ শেষ হয়ে গেছে/)).toBeInTheDocument()
  }, 15000)

  it('confirms admission when the fee is paid', async () => {
    let confirms = 0
    server.use(
      // Like the real server: once confirmed, the application reads as CONFIRMED.
      http.get(`${API}/holds/h_1`, () =>
        HttpResponse.json(confirms > 0 ? heldHold(300, { status: 'CONFIRMED', confirmedAt: isoIn(0) }) : heldHold(300)),
      ),
      http.post(`${API}/holds/h_1/confirm`, async ({ request }) => {
        confirms++
        expect(request.headers.get('X-Student-Id')).toBe(STUDENT)
        await new Promise((resolve) => setTimeout(resolve, 100))
        return HttpResponse.json({ holdId: 'h_1', status: 'CONFIRMED', classId: 'c_1', confirmedAt: isoIn(0) })
      }),
    )
    renderHold()

    await userEvent.dblClick(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' }))

    expect(await screen.findByText('ভর্তি নিশ্চিত হয়েছে।')).toBeInTheDocument()
    expect(screen.getByText('CONFIRMED')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'ভর্তি ফি দিন' })).not.toBeInTheDocument()
    expect(confirms).toBe(1)
  })

  it('shows what went wrong when paying fails because the hold just expired', async () => {
    let gets = 0
    server.use(
      http.get(`${API}/holds/h_1`, () => {
        gets++
        return HttpResponse.json(gets === 1 ? heldHold(300) : heldHold(-5, { status: 'EXPIRED' }))
      }),
      http.post(`${API}/holds/h_1/confirm`, () =>
        HttpResponse.json({ status: 409, code: 'HOLD_EXPIRED' }, { status: 409 }),
      ),
    )
    renderHold()

    await userEvent.click(await screen.findByRole('button', { name: 'ভর্তি ফি দিন' }))

    expect(await screen.findByText('EXPIRED')).toBeInTheDocument()
    expect(screen.getAllByText(/আবেদনের মেয়াদ শেষ হয়ে গেছে/).length).toBeGreaterThan(0)
    expect(payButton()).toBeDisabled()
  })

  it('asks before cancelling, and cancels only after a second confirmation', async () => {
    let deletes = 0
    server.use(
      http.get(`${API}/holds/h_1`, () =>
        HttpResponse.json(deletes > 0 ? heldHold(300, { status: 'CANCELLED' }) : heldHold(300)),
      ),
      http.delete(`${API}/holds/h_1`, () => (deletes++, new HttpResponse(null, { status: 204 }))),
    )
    renderHold()

    await userEvent.click(await screen.findByRole('button', { name: 'বাতিল' }))
    const dialog = screen.getByRole('alertdialog')
    expect(deletes).toBe(0)

    await userEvent.click(within(dialog).getByRole('button', { name: 'না, রেখে দিন' }))
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument()
    expect(deletes).toBe(0)

    await userEvent.click(screen.getByRole('button', { name: 'বাতিল' }))
    await userEvent.click(screen.getByRole('button', { name: 'হ্যাঁ, বাতিল করুন' }))

    expect(await screen.findByText('আবেদনটি বাতিল করা হয়েছে।')).toBeInTheDocument()
    expect(screen.getByText('CANCELLED')).toBeInTheDocument()
    expect(deletes).toBe(1)
  })

  it('is read-only for an application that belongs to someone else', async () => {
    server.use(
      http.get(`${API}/holds/h_1`, () => HttpResponse.json(heldHold(300))),
      http.get(`${API}/me/holds`, () => HttpResponse.json([])),
    )
    renderHold()

    expect(await screen.findByText('এই আবেদন বর্তমান শিক্ষার্থীর নয়।')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'ভর্তি ফি দিন' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'বাতিল' })).not.toBeInTheDocument()
  })

  it('asks for the student ID when none is entered', async () => {
    localStorage.clear()
    server.use(http.get(`${API}/holds/h_1`, () => HttpResponse.json(heldHold(300))))
    renderHold()

    expect(await screen.findByText(/উপরে জন্ম নিবন্ধন নম্বর দিন/)).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'ভর্তি ফি দিন' })).not.toBeInTheDocument()
  })

  it('says so when the application does not exist', async () => {
    server.use(
      http.get(`${API}/holds/h_1`, () => HttpResponse.json({ status: 404, code: 'HOLD_NOT_FOUND' }, { status: 404 })),
    )
    renderHold()

    expect(await screen.findByRole('alert')).toHaveTextContent('আবেদন খুঁজে পাওয়া যায়নি।')
  })

  it('shows a confirmed admission as paid with no pay or cancel buttons', async () => {
    server.use(
      http.get(`${API}/holds/h_1`, () => HttpResponse.json(heldHold(-100, { status: 'CONFIRMED', confirmedAt: isoIn(-50) }))),
      http.get(`${API}/me/holds`, () => HttpResponse.json(mine('CONFIRMED'))),
    )
    renderHold()

    expect(await screen.findByText('ভর্তি নিশ্চিত হয়েছে।')).toBeInTheDocument()
    expect(screen.queryByRole('timer')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'ভর্তি ফি দিন' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'বাতিল' })).not.toBeInTheDocument()
  })
})
