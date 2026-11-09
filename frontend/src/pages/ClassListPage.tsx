import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from 'react-router-dom'
import { createHold } from '../api/endpoints'
import type { MyHold } from '../api/types'
import { ErrorMessage } from '../components/ErrorMessage'
import { useStudentId } from '../hooks/useStudentId'
import { useClasses, useMyHolds } from '../hooks/queries'

const formatTime = (timestamp: number) => new Date(timestamp).toLocaleTimeString('en-GB')

export function ClassListPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { studentId } = useStudentId()
  const classes = useClasses()
  const myHolds = useMyHolds(studentId)

  const apply = useMutation({
    mutationFn: (classId: string) => createHold(classId, studentId!),
    onSuccess: (created) => {
      void queryClient.invalidateQueries({ queryKey: ['classes'] })
      void queryClient.invalidateQueries({ queryKey: ['myHolds'] })
      navigate(`/holds/${created.holdId}`)
    },
    // The counts the user saw were stale (for example the last seat just went): show the truth.
    onError: () => void queryClient.invalidateQueries({ queryKey: ['classes'] }),
  })

  // The student's running application (or confirmed admission) in each class.
  const activeByClass = new Map<string, MyHold>()
  for (const hold of myHolds.data ?? []) {
    if (hold.status === 'HELD' || hold.status === 'CONFIRMED') activeByClass.set(hold.classId, hold)
  }

  return (
    <section>
      <div className="page-head">
        <h1>Class তালিকা</h1>
        {classes.dataUpdatedAt > 0 && (
          <span className="page-head__meta">সর্বশেষ হালনাগাদ: {formatTime(classes.dataUpdatedAt)}</span>
        )}
      </div>

      {classes.isPending && <p className="muted">লোড হচ্ছে…</p>}

      {classes.isError && !classes.data && (
        <div className="card">
          <ErrorMessage error={classes.error} />
          <button type="button" className="btn btn--secondary" onClick={() => classes.refetch()}>
            আবার চেষ্টা করুন
          </button>
        </div>
      )}

      {classes.data && classes.data.length === 0 && (
        <div className="card">
          <p>এখনো কোনো class তৈরি হয়নি।</p>
          <Link to="/classes/new" className="btn">
            Class তৈরি করুন
          </Link>
        </div>
      )}

      {classes.data && classes.data.length > 0 && (
        <>
          {!studentId && (
            <p className="alert alert--info" role="note">
              আবেদন করতে আগে উপরে জন্ম নিবন্ধন নম্বর দিন।
            </p>
          )}
          <div className="table-wrap card" style={{ padding: 0 }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Class</th>
                  <th className="num">মোট seat</th>
                  <th className="num">hold-এ আছে</th>
                  <th className="num">নিশ্চিত</th>
                  <th className="num">খালি</th>
                  <th>আবেদন</th>
                </tr>
              </thead>
              <tbody>
                {classes.data.map((cls) => {
                  const active = activeByClass.get(cls.id)
                  const pending = apply.isPending && apply.variables === cls.id
                  const soldOut = cls.availableSeats === 0
                  return (
                    <tr key={cls.id}>
                      <td>
                        <strong>{cls.name}</strong>
                      </td>
                      <td className="num">{cls.totalSeats}</td>
                      <td className="num">{cls.heldSeats}</td>
                      <td className="num">{cls.confirmedSeats}</td>
                      <td className={`num${soldOut ? ' seats-zero' : ''}`}>{cls.availableSeats}</td>
                      <td>
                        {active ? (
                          <Link to={`/holds/${active.holdId}`}>
                            {active.status === 'CONFIRMED' ? 'ভর্তি নিশ্চিত — দেখুন' : 'আবেদন করা আছে — দেখুন'}
                          </Link>
                        ) : (
                          <button
                            type="button"
                            className="btn btn--small"
                            disabled={!studentId || soldOut || apply.isPending}
                            title={!studentId ? 'আগে উপরে জন্ম নিবন্ধন নম্বর দিন' : soldOut ? 'seat শেষ' : undefined}
                            onClick={() => apply.mutate(cls.id)}
                          >
                            {pending ? 'আবেদন হচ্ছে…' : 'আবেদন করুন'}
                          </button>
                        )}
                        {!active && soldOut && <span className="muted"> seat শেষ</span>}
                        {apply.isError && apply.variables === cls.id && <ErrorMessage error={apply.error} />}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
          <p className="table__note muted">সংখ্যাগুলো প্রতি ৩ সেকেন্ডে নিজে থেকে হালনাগাদ হয়।</p>
        </>
      )}
    </section>
  )
}
