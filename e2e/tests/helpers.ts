import { expect, type APIRequestContext, type Page } from '@playwright/test'

export const API = process.env.E2E_API_URL ?? 'http://localhost:8080'

/** A short random suffix, so repeated runs never collide with data from earlier runs. */
export const unique = () => Math.random().toString(36).slice(2, 8)

/** A random, valid 17-digit birth registration number. */
export function newStudentId(): string {
  let digits = '2015'
  while (digits.length < 17) digits += Math.floor(Math.random() * 10)
  return digits
}

export async function createClassViaApi(request: APIRequestContext, name: string, totalSeats: number) {
  const response = await request.post(`${API}/classes`, { data: { name, totalSeats } })
  expect(response.status()).toBe(201)
  return (await response.json()) as { id: string; name: string }
}

export async function applyViaApi(request: APIRequestContext, classId: string, studentId: string) {
  return request.post(`${API}/classes/${classId}/holds`, { headers: { 'X-Student-Id': studentId } })
}

/** Types the student's number into the box at the top of the page. */
export async function enterStudentId(page: Page, studentId: string) {
  const box = page.getByLabel(/জন্ম নিবন্ধন নম্বর/)
  await box.fill(studentId)
  await expect(page.getByText(/নম্বর সঠিক/)).toBeVisible()
}

export const classRow = (page: Page, name: string) => page.getByRole('row', { name: new RegExp(name) })

/** Seat counts of a class row: total, held, confirmed, available. */
export async function seatCounts(page: Page, name: string) {
  const cells = await classRow(page, name).getByRole('cell').allTextContents()
  return cells.slice(1, 5).map((text) => Number(text.trim()))
}
