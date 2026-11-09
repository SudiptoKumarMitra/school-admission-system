// Shapes of the backend API (field names are fixed by the assignment contract).

export type HoldStatus = 'HELD' | 'CONFIRMED' | 'EXPIRED' | 'CANCELLED'

export interface ClassSummary {
  id: string
  name: string
  totalSeats: number
  heldSeats: number
  confirmedSeats: number
  availableSeats: number
  createdAt?: string
}

export interface CreatedHold {
  holdId: string
  classId: string
  studentId: string
  status: HoldStatus
  expiresAt: string
  className: string
  createdAt: string
  serverTime: string
}

export interface Hold {
  holdId: string
  classId: string
  status: HoldStatus
  expiresAt: string
  className: string
  createdAt: string
  confirmedAt: string | null
  serverTime: string
}

export interface MyHold {
  holdId: string
  classId: string
  className: string
  status: HoldStatus
  expiresAt: string
  createdAt: string
  confirmedAt: string | null
  serverTime: string
}

export interface ConfirmResult {
  holdId: string
  status: HoldStatus
  classId?: string
  confirmedAt?: string | null
}
