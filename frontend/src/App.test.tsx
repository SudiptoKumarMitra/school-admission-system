import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import App from './App'
import { server } from './test/server'
import { renderApp } from './test/utils'

describe('App shell', () => {
  it.each(['/', '/classes/new', '/me', '/holds/h_1', '/nowhere'])(
    'shows the student ID box and the menu on %s',
    (route) => {
      server.use(http.get('http://localhost:8080/classes', () => HttpResponse.json([])))
      renderApp(<App />, { route })

      expect(screen.getByLabelText(/জন্ম নিবন্ধন নম্বর/)).toBeInTheDocument()
      expect(screen.getByRole('navigation', { name: 'প্রধান মেনু' })).toBeInTheDocument()
    },
  )

  it('navigates between the screens from the menu', async () => {
    renderApp(<App />)

    await userEvent.click(screen.getByRole('link', { name: 'Class তৈরি' }))
    expect(screen.getByRole('heading', { name: 'Class তৈরি' })).toBeInTheDocument()

    await userEvent.click(screen.getByRole('link', { name: 'আমার আবেদন' }))
    expect(screen.getByRole('heading', { name: 'আমার আবেদন' })).toBeInTheDocument()
  })

  it('shows a friendly page for an unknown address', () => {
    renderApp(<App />, { route: '/nowhere' })

    expect(screen.getByRole('heading', { name: 'পাতাটি খুঁজে পাওয়া যায়নি' })).toBeInTheDocument()
  })
})
