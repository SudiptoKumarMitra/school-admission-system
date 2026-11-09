import { request } from './client'
import type { ClassSummary, ConfirmResult, CreatedHold, Hold, MyHold } from './types'

export const createClass = (name: string, totalSeats: number) =>
  request<ClassSummary>('/classes', { method: 'POST', body: { name, totalSeats } })

export const listClasses = (signal?: AbortSignal) => request<ClassSummary[]>('/classes', { signal })

export const getClass = (classId: string, signal?: AbortSignal) =>
  request<ClassSummary>(`/classes/${classId}`, { signal })

export const createHold = (classId: string, studentId: string) =>
  request<CreatedHold>(`/classes/${classId}/holds`, { method: 'POST', studentId })

export const getHold = (holdId: string, signal?: AbortSignal) => request<Hold>(`/holds/${holdId}`, { signal })

export const confirmHold = (holdId: string, studentId: string) =>
  request<ConfirmResult>(`/holds/${holdId}/confirm`, { method: 'POST', studentId })

export const cancelHold = (holdId: string, studentId: string) =>
  request<void>(`/holds/${holdId}`, { method: 'DELETE', studentId })

export const listMyHolds = (studentId: string, signal?: AbortSignal) =>
  request<MyHold[]>('/me/holds', { studentId, signal })
