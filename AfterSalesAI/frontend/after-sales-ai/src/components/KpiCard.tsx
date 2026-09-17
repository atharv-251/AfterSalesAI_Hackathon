import { Icon } from './Icon'
import type { IconName } from './Icon'
import { formatNumber } from '../data'

export function KpiCard({ label, value, detail, icon, href, warning = false }: { label: string; value: number; detail: string; icon: IconName; href: string; warning?: boolean }) {
  return <a className={`kpi-card ${warning ? 'kpi-warning' : ''}`} href={href} aria-label={`${label}: ${formatNumber(value)}. View details`}>
    <div className="kpi-top"><span>{label}</span><span className="kpi-icon"><Icon name={icon} /></span></div>
    <strong className="kpi-value">{formatNumber(value)}</strong><span className="kpi-detail">{detail}</span>
    <div className="kpi-bottom"><span>View details</span><Icon name="arrow" size={16} /></div>
  </a>
}
