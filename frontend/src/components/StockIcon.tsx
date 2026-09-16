import type { ReactNode } from 'react'

export type StockIconName =
  | 'plus'
  | 'movement'
  | 'edit'
  | 'in'
  | 'out'
  | 'transfer'
  | 'count'
  | 'archive'
  | 'box'
  | 'close'
  | 'save'

export function StockIcon({ name }: { name: StockIconName }) {
  const paths: Record<StockIconName, ReactNode> = {
    plus: <path d="M12 5v14M5 12h14" />,
    movement: <><path d="M7 7h11l-3-3" /><path d="M17 17H6l3 3" /><path d="m18 7-3 3M6 17l3-3" /></>,
    edit: <><path d="m4 20 4.2-1 10.3-10.3a2.1 2.1 0 0 0-3-3L5.2 16 4 20Z" /><path d="m13.8 7.8 3 3" /></>,
    in: <><path d="M12 3v12" /><path d="m7 10 5 5 5-5" /><path d="M5 21h14" /></>,
    out: <><path d="M12 21V9" /><path d="m7 14 5-5 5 5" /><path d="M5 3h14" /></>,
    transfer: <><path d="M5 8h14l-4-4" /><path d="m19 8-4 4M19 16H5l4 4" /><path d="m5 16 4-4" /></>,
    count: <><path d="M4 5h16v14H4z" /><path d="M8 9h8M8 13h3M14 13h2" /></>,
    archive: <><path d="M4 7h16M6 7l1 13h10l1-13M9 4h6" /><path d="M10 11h4" /></>,
    box: <><path d="m4 7 8-4 8 4-8 4-8-4Z" /><path d="m4 7 8 4 8-4v10l-8 4-8-4V7Z" /><path d="M12 11v10" /></>,
    close: <path d="m6 6 12 12M18 6 6 18" />,
    save: <><path d="M5 4h12l2 2v14H5V4Z" /><path d="M8 4v6h8V4M8 20v-6h8v6" /></>,
  }

  return (
    <svg className="stock-icon" viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <g stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">{paths[name]}</g>
    </svg>
  )
}
