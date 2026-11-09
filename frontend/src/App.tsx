import { Route, Routes } from 'react-router-dom'
import { ErrorBoundary } from './components/ErrorBoundary'
import { Nav } from './components/Nav'
import { ServerStatusBanner } from './components/ServerStatusBanner'
import { StudentIdBar } from './components/StudentIdBar'
import { ClassListPage } from './pages/ClassListPage'
import { CreateClassPage } from './pages/CreateClassPage'
import { HoldPage } from './pages/HoldPage'
import { MyHoldsPage } from './pages/MyHoldsPage'
import { NotFoundPage } from './pages/NotFoundPage'

export default function App() {
  return (
    <div className="app">
      <header className="app-header">
        <div className="container app-header__inner">
          <span className="app-header__brand">School Admission System</span>
          <span className="app-header__tagline">অনলাইন ভর্তি — আগে আবেদন, আগে সুযোগ</span>
        </div>
      </header>
      <StudentIdBar />
      <Nav />
      <ServerStatusBanner />
      <main className="container main">
        <ErrorBoundary>
          <Routes>
            <Route path="/" element={<ClassListPage />} />
            <Route path="/classes/new" element={<CreateClassPage />} />
            <Route path="/holds/:holdId" element={<HoldPage />} />
            <Route path="/me" element={<MyHoldsPage />} />
            <Route path="*" element={<NotFoundPage />} />
          </Routes>
        </ErrorBoundary>
      </main>
    </div>
  )
}
