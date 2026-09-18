export type IconName = 'dashboard' | 'orders' | 'deliveries' | 'claims' | 'inventory' | 'knowledge' | 'assistant' | 'service' | 'arrow' | 'close' | 'menu' | 'sidebar-collapse' | 'sidebar-expand' | 'search' | 'refresh' | 'send' | 'chevron' | 'alert'
const paths: Record<IconName, string> = {
  dashboard: 'M3 3h7v7H3z M14 3h7v4h-7z M14 11h7v10h-7z M3 14h7v7H3z',
  orders: 'M8 4H5v17h14V4h-3 M8 2h8v5H8z M8 11h8 M8 15h5',
  deliveries: 'M3 5h11v12H3z M14 9h4l3 4v4h-7 M5 17a2 2 0 1 0 4 0 M16 17a2 2 0 1 0 4 0',
  claims: 'M12 2l9 4v6c0 5-9 10-9 10S3 17 3 12V6z M12 7v6 M12 17h.01',
  inventory: 'M12 2l10 5v10l-10 5-10-5V7z M2 7l10 5 10-5 M12 12v10 M7 4.5l10 5v5',
  knowledge: 'M12 5C9 2 4 3 2 4v16c3-1 7-1 10 1 3-2 7-2 10-1V4c-2-1-7-2-10 1z M12 5v16',
  assistant: 'M12 3l2.5 6.5L21 12l-6.5 2.5L12 21l-2.5-6.5L3 12l6.5-2.5z M20 2v4 M18 4h4',
  service: 'M4 15l2-6h12l2 6 M3 15h18v5H3z M7 15v5 M17 15v5 M8 12h8 M10 9V6h4v3',
  arrow: 'M4 12h16 M14 6l6 6-6 6', close: 'M6 6l12 12 M18 6L6 18',
  menu: 'M3 6h18 M3 12h18 M3 18h18', 'sidebar-collapse': 'M4 4h16v16H4z M14 9l-3 3 3 3', 'sidebar-expand': 'M4 4h16v16H4z M10 9l3 3-3 3', search: 'M21 21l-6-6 M17 10a7 7 0 1 1-14 0 7 7 0 0 1 14 0',
  refresh: 'M20 7a9 9 0 1 0 1 9 M20 2v6h-6', send: 'M22 2L9 15 M22 2l-7 20-6-7-7-6z',
  chevron: 'M9 5l7 7-7 7', alert: 'M12 3L2 21h20z M12 9v5 M12 17h.01',
}
export function Icon({ name, size = 20 }: { name: IconName; size?: number }) {
  return <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true"><path d={paths[name]} /></svg>
}
