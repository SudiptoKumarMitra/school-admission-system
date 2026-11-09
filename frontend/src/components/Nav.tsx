import { NavLink } from 'react-router-dom'

const links = [
  { to: '/', label: 'Class তালিকা', end: true },
  { to: '/classes/new', label: 'Class তৈরি', end: false },
  { to: '/me', label: 'আমার আবেদন', end: false },
]

export function Nav() {
  return (
    <nav className="nav" aria-label="প্রধান মেনু">
      <div className="container nav__inner">
        {links.map((link) => (
          <NavLink
            key={link.to}
            to={link.to}
            end={link.end}
            className={({ isActive }) => `nav__link${isActive ? ' nav__link--active' : ''}`}
          >
            {link.label}
          </NavLink>
        ))}
      </div>
    </nav>
  )
}
