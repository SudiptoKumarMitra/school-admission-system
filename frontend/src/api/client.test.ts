import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '../test/server'
import { createClass, createHold, cancelHold, listMyHolds } from './endpoints'
import { ApiError, NetworkError, request } from './client'

const API = 'http://localhost:8080'

describe('api client', () => {
  it('parses a JSON success response', async () => {
    server.use(http.get(`${API}/classes`, () => HttpResponse.json([{ id: 'c_1', name: 'A', totalSeats: 1 }])))

    const result = await request<{ id: string }[]>('/classes')

    expect(result[0].id).toBe('c_1')
  })

  it('sends the JSON body for POST /classes', async () => {
    let received: unknown
    server.use(
      http.post(`${API}/classes`, async ({ request: req }) => {
        received = await req.json()
        return HttpResponse.json({ id: 'c_1', name: 'Class 6', totalSeats: 120 }, { status: 201 })
      }),
    )

    await createClass('Class 6', 120)

    expect(received).toEqual({ name: 'Class 6', totalSeats: 120 })
  })

  it('sends the student ID in the X-Student-Id header', async () => {
    let header: string | null = null
    server.use(
      http.post(`${API}/classes/c_1/holds`, ({ request: req }) => {
        header = req.headers.get('X-Student-Id')
        return HttpResponse.json({ holdId: 'h_1' }, { status: 201 })
      }),
      http.get(`${API}/me/holds`, ({ request: req }) => {
        header = req.headers.get('X-Student-Id')
        return HttpResponse.json([])
      }),
    )

    await createHold('c_1', '20152691234567890')
    expect(header).toBe('20152691234567890')

    header = null
    await listMyHolds('11111111111111111')
    expect(header).toBe('11111111111111111')
  })

  it('treats 204 as success with no body', async () => {
    server.use(http.delete(`${API}/holds/h_1`, () => new HttpResponse(null, { status: 204 })))

    await expect(cancelHold('h_1', '20152691234567890')).resolves.toBeUndefined()
  })

  it('turns a problem+json response into an ApiError with the code and field errors', async () => {
    server.use(
      http.post(`${API}/classes`, () =>
        HttpResponse.json(
          { status: 400, code: 'VALIDATION_ERROR', detail: 'Please correct the fields.', errors: { name: ['bad'] } },
          { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
        ),
      ),
    )

    const error = (await createClass('', 1).catch((e) => e)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(400)
    expect(error.code).toBe('VALIDATION_ERROR')
    expect(error.fieldErrors).toEqual({ name: ['bad'] })
  })

  it('still produces an ApiError when the error body is not JSON', async () => {
    server.use(http.get(`${API}/classes`, () => new HttpResponse('Bad gateway', { status: 502 })))

    const error = (await request('/classes').catch((e) => e)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(502)
    expect(error.code).toBe('HTTP_502')
  })

  it('turns a network failure into a NetworkError', async () => {
    server.use(http.get(`${API}/classes`, () => HttpResponse.error()))

    const error = await request('/classes').catch((e) => e)

    expect(error).toBeInstanceOf(NetworkError)
  })

  it('lets the caller cancel a request without reporting a network error', async () => {
    server.use(http.get(`${API}/classes`, async () => new Promise<never>(() => {})))
    const controller = new AbortController()

    const pending = request('/classes', { signal: controller.signal }).catch((e) => e)
    controller.abort()

    const error = await pending
    expect(error).not.toBeInstanceOf(NetworkError)
    expect(error).not.toBeInstanceOf(ApiError)
  })
})
