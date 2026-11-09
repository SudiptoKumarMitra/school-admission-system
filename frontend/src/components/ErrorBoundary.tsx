import { Component, type ErrorInfo, type ReactNode } from 'react'

interface Props {
  children: ReactNode
}

interface State {
  failed: boolean
}

/** Catches rendering errors so the user never sees a blank page. */
export class ErrorBoundary extends Component<Props, State> {
  state: State = { failed: false }

  static getDerivedStateFromError(): State {
    return { failed: true }
  }

  componentDidCatch(error: Error, info: ErrorInfo) {
    console.error('Unexpected UI error', error, info.componentStack)
  }

  render() {
    if (!this.state.failed) return this.props.children
    return (
      <div role="alert" className="error-boundary">
        <h2>কিছু একটা সমস্যা হয়েছে</h2>
        <p>পাতাটি দেখানো যায়নি। আবার চেষ্টা করুন।</p>
        <button type="button" onClick={() => this.setState({ failed: false })}>
          আবার চেষ্টা করুন
        </button>
      </div>
    )
  }
}
