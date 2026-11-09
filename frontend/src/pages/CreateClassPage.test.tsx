import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { server } from '../test/server'
import { renderApp } from '../test/utils'
import { CreateClassPage } from './CreateClassPage'

const API = 'http://localhost:8080'

async function fill(name: string, seats: string) {
  const nameBox = screen.getByLabelText('Class-এর নাম')
  const seatsBox = screen.getByLabelText('মোট seat')
  await userEvent.clear(nameBox)
  await userEvent.clear(seatsBox)
  if (name) await userEvent.type(nameBox, name)
  if (seats) await userEvent.type(seatsBox, seats)
}

const submit = () => userEvent.click(screen.getByRole('button', { name: 'Class তৈরি করুন' }))

describe('CreateClassPage', () => {
  it('shows field errors and sends nothing when the form is empty', async () => {
    let requests = 0
    server.use(http.post(`${API}/classes`, () => (requests++, HttpResponse.json({}, { status: 201 }))))
    renderApp(<CreateClassPage />)

    await submit()

    expect(screen.getByText('Class-এর নাম লিখুন।')).toBeInTheDocument()
    expect(screen.getByText('মোট seat সংখ্যা লিখুন।')).toBeInTheDocument()
    expect(screen.getByLabelText('Class-এর নাম')).toHaveAttribute('aria-invalid', 'true')
    expect(requests).toBe(0)
  })

  it.each(['0', '100001', '12.5'])('rejects %s seats on the client', async (seats) => {
    renderApp(<CreateClassPage />)

    await fill('Class 6', seats)
    await submit()

    expect(screen.getByLabelText('মোট seat')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('creates the class, confirms it, empties the form and refreshes the class list', async () => {
    let body: unknown
    server.use(
      http.post(`${API}/classes`, async ({ request }) => {
        body = await request.json()
        return HttpResponse.json(
          { id: 'c_1', name: 'Class 6 - Morning Shift', totalSeats: 120, heldSeats: 0, confirmedSeats: 0, availableSeats: 120 },
          { status: 201 },
        )
      }),
    )
    const { queryClient } = renderApp(<CreateClassPage />)
    const invalidate = vi.spyOn(queryClient, 'invalidateQueries')

    await fill('  Class 6 - Morning Shift ', '120')
    await submit()

    expect(await screen.findByRole('status')).toHaveTextContent('Class 6 - Morning Shift')
    expect(body).toEqual({ name: 'Class 6 - Morning Shift', totalSeats: 120 })
    expect(screen.getByLabelText('Class-এর নাম')).toHaveValue('')
    expect(screen.getByLabelText('মোট seat')).toHaveValue(null)
    expect(invalidate).toHaveBeenCalledWith({ queryKey: ['classes'] })
  })

  it('can be submitted with the Enter key', async () => {
    server.use(
      http.post(`${API}/classes`, () =>
        HttpResponse.json({ id: 'c_1', name: 'Enter', totalSeats: 3 }, { status: 201 }),
      ),
    )
    renderApp(<CreateClassPage />)

    await fill('Enter', '3')
    await userEvent.type(screen.getByLabelText('মোট seat'), '{Enter}')

    expect(await screen.findByRole('status')).toHaveTextContent('Enter')
  })

  it('does not send two requests when the button is pressed twice quickly', async () => {
    let requests = 0
    server.use(
      http.post(`${API}/classes`, async () => {
        requests++
        await new Promise((resolve) => setTimeout(resolve, 150))
        return HttpResponse.json({ id: 'c_1', name: 'Once', totalSeats: 3 }, { status: 201 })
      }),
    )
    renderApp(<CreateClassPage />)
    await fill('Once', '3')

    const button = screen.getByRole('button', { name: 'Class তৈরি করুন' })
    await userEvent.dblClick(button)

    await screen.findByRole('status')
    expect(requests).toBe(1)
  })

  it('shows a readable message when the server rejects the input', async () => {
    server.use(
      http.post(`${API}/classes`, () =>
        HttpResponse.json(
          { status: 400, code: 'VALIDATION_ERROR', errors: { totalSeats: ['bad'] } },
          { status: 400 },
        ),
      ),
    )
    renderApp(<CreateClassPage />)

    await fill('Class 6', '5')
    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('তথ্য সঠিক নয়')
    expect(screen.getByRole('alert')).toHaveTextContent('মোট seat')
  })

  it('tells the user when the server is down and keeps what they typed', async () => {
    server.use(http.post(`${API}/classes`, () => HttpResponse.error()))
    renderApp(<CreateClassPage />)

    await fill('Class 6', '5')
    await submit()

    expect(await screen.findByRole('alert')).toHaveTextContent('Server-এর সাথে যোগাযোগ করা যাচ্ছে না')
    expect(screen.getByLabelText('Class-এর নাম')).toHaveValue('Class 6')
    await waitFor(() => expect(screen.getByRole('button', { name: 'Class তৈরি করুন' })).toBeEnabled())
  })
})
