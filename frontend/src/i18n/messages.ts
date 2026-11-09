import { ApiError, NetworkError } from '../api/client'
import type { HoldStatus } from '../api/types'

/** Message shown for each machine-readable error code returned by the API. */
export const errorMessages: Record<string, string> = {
  NO_SEATS_AVAILABLE: 'এই class-এ আর কোনো খালি seat নেই।',
  ACTIVE_HOLD_EXISTS: 'এই class-এ আপনার একটি চলমান আবেদন আছে।',
  ALREADY_CONFIRMED: 'এই class-এ আপনার ভর্তি ইতিমধ্যে নিশ্চিত হয়েছে।',
  HOLD_EXPIRED: 'আবেদনের মেয়াদ শেষ হয়ে গেছে। চাইলে আবার আবেদন করুন।',
  HOLD_CANCELLED: 'এই আবেদনটি বাতিল করা হয়েছে।',
  CANNOT_CANCEL_CONFIRMED: 'নিশ্চিত হওয়া ভর্তি বাতিল করা যায় না।',
  NOT_HOLD_OWNER: 'এই আবেদনটি আপনার নয়।',
  CLASS_NOT_FOUND: 'Class খুঁজে পাওয়া যায়নি।',
  HOLD_NOT_FOUND: 'আবেদন খুঁজে পাওয়া যায়নি।',
  STUDENT_ID_REQUIRED: 'সঠিক ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।',
  INVALID_STUDENT_ID: 'সঠিক ১৭ অঙ্কের জন্ম নিবন্ধন নম্বর দিন।',
}

const fieldMessages: Record<string, string> = {
  name: "Class-এর নাম ১ থেকে ১০০ অক্ষরের মধ্যে হতে হবে।",
  totalSeats: 'মোট seat ১ থেকে ১,০০,০০০-এর মধ্যে একটি পূর্ণ সংখ্যা হতে হবে।',
}

export const networkErrorMessage =
  'Server-এর সাথে যোগাযোগ করা যাচ্ছে না। ইন্টারনেট বা server চালু আছে কিনা দেখুন।'
export const serverErrorMessage = 'Server-এ একটি সমস্যা হয়েছে। কিছুক্ষণ পর আবার চেষ্টা করুন।'
const validationMessage = 'তথ্য সঠিক নয়।'

/** Turns any thrown value into a message a parent or school admin can understand. */
export function messageForError(error: unknown): string {
  if (error instanceof NetworkError) return networkErrorMessage

  if (error instanceof ApiError) {
    if (error.code === 'VALIDATION_ERROR') {
      const details = Object.keys(error.fieldErrors ?? {})
        .map((field) => fieldMessages[field])
        .filter(Boolean)
      return details.length ? `${validationMessage} ${details.join(' ')}` : validationMessage
    }
    if (errorMessages[error.code]) return errorMessages[error.code]
  }

  return serverErrorMessage
}

/** Bangla gloss shown next to the English status code. */
export const statusLabels: Record<HoldStatus, string> = {
  HELD: 'seat hold-এ আছে',
  CONFIRMED: 'ভর্তি নিশ্চিত',
  EXPIRED: 'মেয়াদ শেষ',
  CANCELLED: 'বাতিল করা হয়েছে',
}
