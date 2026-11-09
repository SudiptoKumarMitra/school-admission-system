import { Link } from 'react-router-dom'

export function NotFoundPage() {
  return (
    <section className="card">
      <h1>পাতাটি খুঁজে পাওয়া যায়নি</h1>
      <p>
        <Link to="/">Class তালিকায় ফিরে যান</Link>
      </p>
    </section>
  )
}
