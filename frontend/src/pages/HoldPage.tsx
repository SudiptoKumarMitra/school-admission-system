import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { cancelHold, confirmHold } from '../api/endpoints'
import { ApiError } from '../api/client'
import type { Hold, HoldStatus } from '../api/types'
import { Countdown } from '../components/Countdown'
import { ErrorMessage } from '../components/ErrorMessage'
import { StatusBadge } from '../components/StatusBadge'
import { useCountdown } from '../hooks/useCountdown'
import { useStudentId } from '../hooks/useStudentId'
import { useHold, useMyHolds } from '../hooks/queries'

const formatDateTime = (iso: string) => new Date(iso).toLocaleString('en-GB')

export function HoldPage() {
  const { holdId } = useParams()
  const queryClient = useQueryClient()
  const { studentId } = useStudentId()
  const hold = useHold(holdId)
  const myHolds = useMyHolds(studentId)
  const [confirmingCancel, setConfirmingCancel] = useState(false)

  const remaining = useCountdown(hold.data?.expiresAt, hold.data?.serverTime, hold.dataUpdatedAt)

  // The countdown is the authority the moment time runs out: no need to wait for the next poll.
  const expiredLocally = hold.data?.status === 'HELD' && remaining === 0
  const status: HoldStatus | undefined = expiredLocally ? 'EXPIRED' : hold.data?.status

  useEffect(() => {
    if (!expiredLocally) return
    void queryClient.invalidateQueries({ queryKey: ['hold', holdId] })
    void queryClient.invalidateQueries({ queryKey: ['classes'] })
    void queryClient.invalidateQueries({ queryKey: ['myHolds'] })
  }, [expiredLocally, holdId, queryClient])

  const refreshAll = () => {
    void queryClient.invalidateQueries({ queryKey: ['hold', holdId] })
    void queryClient.invalidateQueries({ queryKey: ['classes'] })
    void queryClient.invalidateQueries({ queryKey: ['myHolds'] })
  }

  const setStatus = (next: HoldStatus, extra: Partial<Hold> = {}) =>
    queryClient.setQueryData<Hold>(['hold', holdId], (old) => (old ? { ...old, status: next, ...extra } : old))

  const pay = useMutation({
    mutationFn: () => confirmHold(holdId!, studentId!),
    onSuccess: (result) => {
      setStatus('CONFIRMED', { confirmedAt: result.confirmedAt ?? null })
      refreshAll()
    },
    onError: (error) => {
      // Expired or cancelled in the meantime: show the real state.
      if (error instanceof ApiError) refreshAll()
    },
  })

  const cancel = useMutation({
    mutationFn: () => cancelHold(holdId!, studentId!),
    onSuccess: () => {
      setConfirmingCancel(false)
      setStatus('CANCELLED')
      refreshAll()
    },
    onError: (error) => {
      setConfirmingCancel(false)
      if (error instanceof ApiError) refreshAll()
    },
  })

  if (hold.isPending) return <p className="muted">লোড হচ্ছে…</p>

  if (hold.isError || !hold.data) {
    return (
      <section className="card">
        <h1>আবেদন</h1>
        <ErrorMessage error={hold.error} />
        <div className="actions">
          <button type="button" className="btn btn--secondary" onClick={() => hold.refetch()}>
            আবার চেষ্টা করুন
          </button>
          <Link to="/" className="btn btn--ghost">
            Class তালিকা
          </Link>
        </div>
      </section>
    )
  }

  const data = hold.data
  const ownershipKnown = myHolds.isSuccess
  const isOwner = ownershipKnown && (myHolds.data ?? []).some((item) => item.holdId === data.holdId)
  const timeLeft = status === 'HELD' && remaining !== null && remaining > 0
  const busy = pay.isPending || cancel.isPending
  const canAct = isOwner && timeLeft && !busy

  return (
    <section className="card">
      <h1>আবেদন</h1>

      <dl className="details">
        <dt>Class</dt>
        <dd>{data.className}</dd>
        <dt>অবস্থা</dt>
        <dd>{status && <StatusBadge status={status} />}</dd>
        <dt>আবেদনের সময়</dt>
        <dd>{formatDateTime(data.createdAt)}</dd>
        <dt>মেয়াদ শেষ হবে</dt>
        <dd>{formatDateTime(data.expiresAt)}</dd>
        {timeLeft && remaining !== null && (
          <>
            <dt>মেয়াদ শেষ হতে বাকি</dt>
            <dd>
              <Countdown remainingMs={remaining} />
            </dd>
          </>
        )}
      </dl>

      {status === 'HELD' && (
        <p className="muted">মেয়াদের মধ্যে ভর্তি ফি দিন, নইলে seat অন্য আবেদনকারীর জন্য খুলে যাবে।</p>
      )}
      {status === 'CONFIRMED' && (
        <p className="alert alert--success" role="status">
          ভর্তি নিশ্চিত হয়েছে।
        </p>
      )}
      {status === 'EXPIRED' && (
        <p className="alert alert--info" role="status">
          আবেদনের মেয়াদ শেষ হয়ে গেছে, seat ছেড়ে দেওয়া হয়েছে। চাইলে <Link to="/">Class তালিকা</Link> থেকে আবার
          আবেদন করুন।
        </p>
      )}
      {status === 'CANCELLED' && (
        <p className="alert alert--info" role="status">
          আবেদনটি বাতিল করা হয়েছে।
        </p>
      )}

      {!studentId && (
        <p className="alert alert--info" role="note">
          ভর্তি ফি দিতে বা আবেদন বাতিল করতে উপরে জন্ম নিবন্ধন নম্বর দিন।
        </p>
      )}
      {studentId && ownershipKnown && !isOwner && (
        <p className="alert alert--info" role="note">
          এই আবেদন বর্তমান শিক্ষার্থীর নয়।
        </p>
      )}

      <ErrorMessage error={pay.error ?? cancel.error} />

      {confirmingCancel && canAct && (
        <div className="alert alert--info" role="alertdialog" aria-label="বাতিল নিশ্চিত করুন">
          <p>আবেদন বাতিল করলে seat ছেড়ে দেওয়া হবে। নিশ্চিত?</p>
          <div className="actions">
            <button type="button" className="btn btn--danger btn--small" onClick={() => cancel.mutate()}>
              হ্যাঁ, বাতিল করুন
            </button>
            <button type="button" className="btn btn--secondary btn--small" onClick={() => setConfirmingCancel(false)}>
              না, রেখে দিন
            </button>
          </div>
        </div>
      )}

      {isOwner && status !== 'CONFIRMED' && status !== 'CANCELLED' && (
        <div className="actions">
          <button
            type="button"
            className="btn"
            disabled={!canAct}
            onClick={() => {
              setConfirmingCancel(false)
              pay.mutate()
            }}
          >
            {pay.isPending ? 'প্রক্রিয়া চলছে…' : 'ভর্তি ফি দিন'}
          </button>
          {timeLeft && (
            <button
              type="button"
              className="btn btn--danger"
              disabled={!canAct || confirmingCancel}
              onClick={() => setConfirmingCancel(true)}
            >
              বাতিল
            </button>
          )}
        </div>
      )}

      <p className="actions">
        <Link to="/me">আমার সব আবেদন</Link>
      </p>
    </section>
  )
}
