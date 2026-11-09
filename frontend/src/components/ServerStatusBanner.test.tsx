import { useQuery } from '@tanstack/react-query'
import { screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { listClasses } from '../api/endpoints'
import { server } from '../test/server'
import { renderApp } from '../test/utils'
import { ServerStatusBanner } from './ServerStatusBanner'

function Probe() {
  const { refetch } = useQuery({ queryKey: ['classes'], queryFn: ({ signal }) => listClasses(signal) })
  return <button onClick={() => refetch()}>reload</button>
}

describe('ServerStatusBanner', () => {
  it('stays hidden while the server answers, even with an error answer', async () => {
    server.use(
      http.get('http://localhost:8080/classes', () =>
        HttpResponse.json({ code: 'INTERNAL_ERROR' }, { status: 500 }),
      ),
    )
    renderApp(
      <>
        <ServerStatusBanner />
        <Probe />
      </>,
    )

    await screen.findByText('reload')
    await new Promise((resolve) => setTimeout(resolve, 100))

    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('appears when the server cannot be reached and disappears when it is back', async () => {
    server.use(http.get('http://localhost:8080/classes', () => HttpResponse.error()))
    renderApp(
      <>
        <ServerStatusBanner />
        <Probe />
      </>,
    )

    expect(await screen.findByRole('alert')).toHaveTextContent('Server-এর সাথে যোগাযোগ করা যাচ্ছে না')

    server.use(http.get('http://localhost:8080/classes', () => HttpResponse.json([])))
    screen.getByText('reload').click()

    await screen.findByText('reload')
    await expect.poll(() => screen.queryByRole('alert')).toBeNull()
  })
})
