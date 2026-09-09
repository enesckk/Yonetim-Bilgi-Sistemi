import { Link } from 'react-router-dom'

export function PageBackLink({ to, children }: { to: string; children: React.ReactNode }) {
  return (
    <Link to={to} className="page-back">
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" aria-hidden="true">
        <path
          d="M15 6l-6 6 6 6"
          stroke="currentColor"
          strokeWidth="1.85"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
      <span>{children}</span>
    </Link>
  )
}
