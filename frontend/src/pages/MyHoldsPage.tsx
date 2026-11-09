import { Link } from 'react-router-dom'
import type { MyHold } from '../api/types'
import { Countdown } from '../components/Countdown'
import { ErrorMessage } from '../components/ErrorMessage'
import { StatusBadge } from '../components/StatusBadge'
import { useCountdown } from '../hooks/useCountdown'
import { useStudentId } from '../hooks/useStudentId'
import { useMyHolds } from '../hooks/queries'

const formatDateTime = (iso: string) => new Date(iso).toLocaleString('en-GB')

function MyHoldRow({ hold, receivedAt }: { hold: MyHold; receivedAt: number }) {
  const remaining = useCountdown(
    hold.status === 'HELD' ? hold.expiresAt : undefined,
    hold.serverTime,
    receivedAt,
  )
  // When the countdown ends the row shows EXPIRED at once, without waiting for the next refresh.
  const status = hold.status === 'HELD' && remaining === 0 ? 'EXPIRED' : hold.status
  const active = status === 'HELD'

  return (
    <tr className={active ? 'my-hold--active' : undefined}>
      <td>
        <strong>{hold.className}</strong>
        {active && <div className="muted">চলমান আবেদন</div>}
      </td>
      <td>
        <StatusBadge status={status} />
      </td>
      <td>
        {active && remaining !== null ? (
          <>
            বাকি: <Countdown remainingMs={remaining} />
          </>
        ) : (
          <span className="muted">{formatDateTime(hold.createdAt)}</span>
        )}
      </td>
      <td>
        <Link to={`/holds/${hold.holdId}`}>দেখুন</Link>
      </td>
    </tr>
  )
}

export function MyHoldsPage() {
  const { studentId } = useStudentId()
  const myHolds = useMyHolds(studentId)

  if (!studentId) {
    return (
      <section className="card">
        <h1>আমার আবেদন</h1>
        <p className="alert alert--info" role="note">
          আপনার আবেদনগুলো দেখতে উপরে জন্ম নিবন্ধন নম্বর দিন।
        </p>
      </section>
    )
  }

  // Running applications first, so the one that needs action is the first thing the student sees.
  const sorted = [...(myHolds.data ?? [])].sort(
    (a, b) => Number(b.status === 'HELD') - Number(a.status === 'HELD'),
  )

  return (
    <section>
      <div className="page-head">
        <h1>আমার আবেদন</h1>
      </div>

      {myHolds.isPending && <p className="muted">লোড হচ্ছে…</p>}

      {myHolds.isError && !myHolds.data && (
        <div className="card">
          <ErrorMessage error={myHolds.error} />
          <button type="button" className="btn btn--secondary" onClick={() => myHolds.refetch()}>
            আবার চেষ্টা করুন
          </button>
        </div>
      )}

      {myHolds.data && sorted.length === 0 && (
        <div className="card">
          <p>এই শিক্ষার্থীর এখনো কোনো আবেদন নেই।</p>
          <Link to="/" className="btn">
            Class তালিকা দেখুন
          </Link>
        </div>
      )}

      {sorted.length > 0 && (
        <div className="table-wrap card" style={{ padding: 0 }}>
          <table className="table">
            <thead>
              <tr>
                <th>Class</th>
                <th>অবস্থা</th>
                <th>সময়</th>
                <th>বিস্তারিত</th>
              </tr>
            </thead>
            <tbody>
              {sorted.map((hold) => (
                <MyHoldRow key={hold.holdId} hold={hold} receivedAt={myHolds.dataUpdatedAt} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  )
}
