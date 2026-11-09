export const MAX_CLASS_NAME_LENGTH = 100
export const MIN_SEATS = 1
export const MAX_SEATS = 100_000

export interface ClassFormErrors {
  name?: string
  totalSeats?: string
}

export interface ParsedClassForm {
  name: string
  totalSeats: number
}

/** Validates the create-class form the same way the server does. */
export function validateClassForm(
  rawName: string,
  rawSeats: string,
): { values: ParsedClassForm | null; errors: ClassFormErrors } {
  const errors: ClassFormErrors = {}

  const name = rawName.trim()
  if (name.length === 0) errors.name = 'Class-এর নাম লিখুন।'
  else if (name.length > MAX_CLASS_NAME_LENGTH) errors.name = `নাম সর্বোচ্চ ${MAX_CLASS_NAME_LENGTH} অক্ষরের হতে পারে।`

  const seatsText = rawSeats.trim()
  const totalSeats = Number(seatsText)
  if (seatsText.length === 0) errors.totalSeats = 'মোট seat সংখ্যা লিখুন।'
  else if (!/^\d+$/.test(seatsText)) errors.totalSeats = 'মোট seat একটি পূর্ণ সংখ্যা হতে হবে (দশমিক বা অক্ষর নয়)।'
  else if (totalSeats < MIN_SEATS || totalSeats > MAX_SEATS)
    errors.totalSeats = `মোট seat ${MIN_SEATS} থেকে ${MAX_SEATS}-এর মধ্যে হতে হবে।`

  return Object.keys(errors).length ? { values: null, errors } : { values: { name, totalSeats }, errors }
}
