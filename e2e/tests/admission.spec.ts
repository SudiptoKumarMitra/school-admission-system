import { expect, test } from '@playwright/test'
import {
  API,
  applyViaApi,
  classRow,
  createClassViaApi,
  enterStudentId,
  newStudentId,
  seatCounts,
  unique,
} from './helpers'

test.describe('school admin', () => {
  test('creates a class from the UI and sees it in the class list', async ({ page }) => {
    const name = `E2E Class ${unique()}`
    await page.goto('/')

    await page.getByRole('link', { name: 'Class তৈরি' }).click()
    await page.getByLabel('Class-এর নাম').fill(name)
    await page.getByLabel('মোট seat').fill('120')
    await page.getByRole('button', { name: 'Class তৈরি করুন' }).click()
    await expect(page.getByRole('status')).toContainText(name)

    await page.getByRole('link', { name: 'Class তালিকা দেখুন' }).click()
    await expect(classRow(page, name)).toBeVisible()
    expect(await seatCounts(page, name)).toEqual([120, 0, 0, 120])
  })

  test('is told what is wrong when the class form is incomplete', async ({ page }) => {
    await page.goto('/classes/new')

    await page.getByRole('button', { name: 'Class তৈরি করুন' }).click()

    await expect(page.getByText('Class-এর নাম লিখুন।')).toBeVisible()
    await expect(page.getByText('মোট seat সংখ্যা লিখুন।')).toBeVisible()
  })
})

test.describe('parent', () => {
  test('applies, sees the countdown, pays the fee and the admission is confirmed', async ({ page, request }) => {
    const name = `E2E Pay ${unique()}`
    await createClassViaApi(request, name, 3)
    await page.goto('/')
    await enterStudentId(page, newStudentId())

    await classRow(page, name).getByRole('button', { name: 'আবেদন করুন' }).click()

    await expect(page).toHaveURL(/\/holds\/h_/)
    await expect(page.getByText('HELD', { exact: true })).toBeVisible()
    await expect(page.getByRole('timer')).toBeVisible()

    await page.getByRole('button', { name: 'ভর্তি ফি দিন' }).click()
    await expect(page.getByText('ভর্তি নিশ্চিত হয়েছে।')).toBeVisible()
    await expect(page.getByText('CONFIRMED', { exact: true })).toBeVisible()
    await expect(page.getByRole('button', { name: 'ভর্তি ফি দিন' })).toHaveCount(0)

    await page.getByRole('link', { name: 'Class তালিকা' }).first().click()
    await expect.poll(() => seatCounts(page, name)).toEqual([3, 0, 1, 2])
    await expect(classRow(page, name).getByRole('link', { name: 'ভর্তি নিশ্চিত — দেখুন' })).toBeVisible()
  })

  test('an unpaid application expires by itself, with no reload, and the seat comes back', async ({ page, request }) => {
    const name = `E2E Expire ${unique()}`
    await createClassViaApi(request, name, 1)
    await page.goto('/')
    await enterStudentId(page, newStudentId())
    await classRow(page, name).getByRole('button', { name: 'আবেদন করুন' }).click()
    await expect(page).toHaveURL(/\/holds\/h_/)
    await expect(page.getByText('HELD', { exact: true })).toBeVisible()

    // How long does a hold last on this stack? (start it with HOLD_DURATION_SECONDS=15 for a quick run)
    const holdId = page.url().split('/').pop()!
    const hold = await (await request.get(`${API}/holds/${holdId}`)).json()
    const seconds = (Date.parse(hold.expiresAt) - Date.parse(hold.serverTime)) / 1000
    test.skip(seconds > 40, `hold lasts ${Math.round(seconds)} s: start the stack with HOLD_DURATION_SECONDS=15`)

    // From here on nobody touches the page: no reload, no click.
    let loads = 0
    page.on('load', () => loads++)

    await expect(page.getByText('EXPIRED', { exact: true })).toBeVisible({ timeout: (seconds + 10) * 1000 })
    await expect(page.getByRole('button', { name: 'ভর্তি ফি দিন' })).toBeDisabled()
    await expect(page.getByRole('timer')).toHaveCount(0)
    expect(loads).toBe(0)

    await page.getByRole('link', { name: 'Class তালিকা' }).first().click()
    await expect.poll(() => seatCounts(page, name)).toEqual([1, 0, 0, 1])
    await expect(classRow(page, name).getByRole('button', { name: 'আবেদন করুন' })).toBeEnabled()
  })

  test('cancelling an application frees the seat', async ({ page, request }) => {
    const name = `E2E Cancel ${unique()}`
    await createClassViaApi(request, name, 1)
    await page.goto('/')
    await enterStudentId(page, newStudentId())
    await classRow(page, name).getByRole('button', { name: 'আবেদন করুন' }).click()
    await expect(page.getByText('HELD', { exact: true })).toBeVisible()

    await page.getByRole('button', { name: 'বাতিল' }).click()
    await expect(page.getByRole('alertdialog')).toBeVisible() // asks first
    await page.getByRole('button', { name: 'হ্যাঁ, বাতিল করুন' }).click()

    await expect(page.getByText('আবেদনটি বাতিল করা হয়েছে।')).toBeVisible()
    await page.getByRole('link', { name: 'Class তালিকা' }).first().click()
    await expect.poll(() => seatCounts(page, name)).toEqual([1, 0, 0, 1])
  })

  test('the running application is still there after a reload and after reopening the browser', async ({
    page,
    context,
    request,
  }) => {
    const name = `E2E Persist ${unique()}`
    await createClassViaApi(request, name, 2)
    await page.goto('/')
    await enterStudentId(page, newStudentId())
    await classRow(page, name).getByRole('button', { name: 'আবেদন করুন' }).click()
    await expect(page.getByText('HELD', { exact: true })).toBeVisible()

    await page.reload()
    await expect(page.getByText('Class', { exact: false }).first()).toBeVisible()
    await page.getByRole('link', { name: 'আমার আবেদন' }).click()
    await expect(page.getByRole('row', { name: new RegExp(name) })).toContainText('চলমান আবেদন')

    // "Reopen the browser": a brand new page in the same browser profile.
    const reopened = await context.newPage()
    await reopened.goto('/me')
    await expect(reopened.getByLabel(/জন্ম নিবন্ধন নম্বর/)).not.toHaveValue('')
    await expect(reopened.getByRole('row', { name: new RegExp(name) })).toContainText('HELD')
  })

  test('sees seat counts change live, without a reload, and a sold-out class becomes unavailable', async ({
    page,
    request,
  }) => {
    const name = `E2E Live ${unique()}`
    const created = await createClassViaApi(request, name, 1)
    await page.goto('/')
    await enterStudentId(page, newStudentId())
    await expect(classRow(page, name).getByRole('button', { name: 'আবেদন করুন' })).toBeEnabled()
    expect(await seatCounts(page, name)).toEqual([1, 0, 0, 1])

    // Another parent takes the only seat (from elsewhere); this page is not touched.
    const other = await applyViaApi(request, created.id, newStudentId())
    expect(other.status()).toBe(201)

    await expect.poll(() => seatCounts(page, name), { timeout: 10_000 }).toEqual([1, 1, 0, 0])
    await expect(classRow(page, name).getByRole('button', { name: 'আবেদন করুন' })).toBeDisabled()
    await expect(classRow(page, name)).toContainText('seat শেষ')
  })

  test('a second parent cannot get a seat that is already taken', async ({ page, request }) => {
    const name = `E2E SoldOut ${unique()}`
    const created = await createClassViaApi(request, name, 1)
    await applyViaApi(request, created.id, newStudentId())
    await page.goto('/')
    await enterStudentId(page, newStudentId())

    await expect(classRow(page, name).getByRole('button', { name: 'আবেদন করুন' })).toBeDisabled()
    const response = await applyViaApi(request, created.id, newStudentId())
    expect(response.status()).toBe(409)
  })

  test('is told in plain language when the last seat is gone at the moment of applying', async ({
    page,
    request,
  }) => {
    const name = `E2E Race ${unique()}`
    await createClassViaApi(request, name, 5)
    await page.goto('/')
    await enterStudentId(page, newStudentId())
    // The server answers "no seats" even though the list the parent sees still shows seats.
    await page.route(/\/classes\/c_[0-9a-f]+\/holds/, (route) =>
      route.fulfill({
        status: 409,
        contentType: 'application/problem+json',
        body: JSON.stringify({ status: 409, code: 'NO_SEATS_AVAILABLE', title: 'No seats available' }),
      }),
    )

    await classRow(page, name).getByRole('button', { name: 'আবেদন করুন' }).click()

    await expect(page.getByRole('alert')).toContainText('এই class-এ আর কোনো খালি seat নেই।')
    await expect(page).toHaveURL(/\/$/)
  })

  test('needs a student ID before applying and says so', async ({ page, request }) => {
    const name = `E2E NoId ${unique()}`
    await createClassViaApi(request, name, 2)
    await page.goto('/')

    await expect(page.getByText('আবেদন করতে আগে উপরে জন্ম নিবন্ধন নম্বর দিন।')).toBeVisible()
    await expect(classRow(page, name).getByRole('button', { name: 'আবেদন করুন' })).toBeDisabled()

    await page.getByLabel(/জন্ম নিবন্ধন নম্বর/).fill('12345')
    await expect(page.getByText(/17 অঙ্কের নম্বর দিন/)).toBeVisible()
  })
})

test.describe('when the server is down', () => {
  test('shows a clear message instead of a blank page, then recovers by itself', async ({ page }) => {
    await page.route(/:8080\//, (route) => route.abort())
    await page.goto('/')

    await expect(page.getByRole('alert').first()).toContainText('Server-এর সাথে যোগাযোগ করা যাচ্ছে না')
    await expect(page.getByRole('heading', { name: 'Class তালিকা' })).toBeVisible()

    await page.unroute(/:8080\//)
    await expect(page.getByText('Server-এর সাথে যোগাযোগ করা যাচ্ছে না', { exact: false })).toHaveCount(0, {
      timeout: 15_000,
    })
  })
})
