import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { StudentIdBar } from './StudentIdBar'
import { useStudentId } from '../hooks/useStudentId'
import { renderApp } from '../test/utils'

function Identity() {
  const { studentId } = useStudentId()
  return <output data-testid="identity">{studentId ?? 'none'}</output>
}

const box = () => screen.getByLabelText(/জন্ম নিবন্ধন নম্বর/) as HTMLInputElement

describe('StudentIdBar', () => {
  it('asks for the number when the box is empty', () => {
    renderApp(<StudentIdBar />)

    expect(screen.getByText(/জন্ম নিবন্ধন নম্বর দিন/)).toBeInTheDocument()
    expect(box()).toHaveAttribute('inputmode', 'numeric')
  })

  it('rejects an incomplete number with a message and no identity', async () => {
    renderApp(
      <>
        <StudentIdBar />
        <Identity />
      </>,
    )

    await userEvent.type(box(), '12345')

    expect(screen.getByText(/17 অঙ্কের নম্বর দিন \(এখন 5 অঙ্ক/)).toBeInTheDocument()
    expect(box()).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByTestId('identity')).toHaveTextContent('none')
  })

  it('accepts exactly 17 digits and exposes them as the identity', async () => {
    renderApp(
      <>
        <StudentIdBar />
        <Identity />
      </>,
    )

    await userEvent.type(box(), '20152691234567890')

    expect(screen.getByText(/নম্বর সঠিক/)).toBeInTheDocument()
    expect(screen.getByTestId('identity')).toHaveTextContent('20152691234567890')
  })

  it('converts Bangla digits and ignores letters', async () => {
    renderApp(<StudentIdBar />)

    await userEvent.type(box(), '২০১৫ab২৬৯১২৩৪৫৬৭৮৯০')

    expect(box().value).toBe('20152691234567890')
  })

  it('does not accept more than 17 digits', async () => {
    renderApp(<StudentIdBar />)

    await userEvent.type(box(), '201526912345678901234')

    expect(box().value).toHaveLength(17)
  })

  it('remembers a valid number across a reload (remount)', async () => {
    const first = renderApp(<StudentIdBar />)
    await userEvent.type(box(), '20152691234567890')
    expect(localStorage.getItem('studentId')).toBe('20152691234567890')
    first.unmount()

    renderApp(<StudentIdBar />)

    expect(box().value).toBe('20152691234567890')
  })

  it('does not store an incomplete number', async () => {
    renderApp(<StudentIdBar />)

    await userEvent.type(box(), '2015')

    expect(localStorage.getItem('studentId')).toBeNull()
  })

  it('clears the box and the stored number', async () => {
    renderApp(<StudentIdBar />)
    await userEvent.type(box(), '20152691234567890')

    await userEvent.click(screen.getByRole('button', { name: 'মুছুন' }))

    expect(box().value).toBe('')
    expect(localStorage.getItem('studentId')).toBeNull()
  })
})
