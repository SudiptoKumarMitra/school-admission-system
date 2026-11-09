import { useServerReachable } from '../api/connectionStatus'

export function ServerStatusBanner() {
  const reachable = useServerReachable()
  if (reachable) return null

  return (
    <div className="banner banner--warning" role="alert">
      <div className="container">
        Server-এর সাথে যোগাযোগ করা যাচ্ছে না। কিছুক্ষণ পর আপনা-আপনি আবার চেষ্টা করা হবে।
      </div>
    </div>
  )
}
