import { Dialog } from './Dialog'
import { Icon } from './Icon'
import type { IconName } from './Icon'
import { routeHref } from '../navigation'
import type { Page } from '../navigation'

const tenant1Items: { page: Page; label: string; icon: IconName }[] = [
  { page: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
  { page: 'orders', label: 'Orders', icon: 'orders' },
  { page: 'deliveries', label: 'Deliveries', icon: 'deliveries' },
  { page: 'claims', label: 'Claims', icon: 'claims' },
  { page: 'inventory', label: 'Parts / Inventory', icon: 'inventory' },
  { page: 'knowledge', label: 'Knowledge / SOP', icon: 'knowledge' },
  { page: 'assistant', label: 'AI Assistant', icon: 'assistant' },
]
const tenant2Items: { page: Page; label: string; icon: IconName }[] = [
  { page: 'dashboard', label: 'Dashboard', icon: 'dashboard' },
  { page: 'service-overview', label: 'Service overview', icon: 'deliveries' },
  { page: 'repair-status', label: 'Repair status', icon: 'orders' },
  { page: 'warranty-summary', label: 'Warranty summary', icon: 'claims' },
  { page: 'assistant', label: 'AI Assistant', icon: 'assistant' },
]

export function Sidebar({ page, tenant, collapsed, mobileOpen, onClose, onCollapse, onAssistant }: { page: Page; tenant: 1 | 2; collapsed: boolean; mobileOpen: boolean; onClose: () => void; onCollapse: () => void; onAssistant: () => void }) {
  const items = tenant === 1 ? tenant1Items : tenant2Items
  const content = <>
    <a className="brand" href={routeHref('dashboard')} onClick={onClose}><span className="brand-mark"><Icon name="inventory" size={26} /></span><span>SMART WORKSHOP<small>AFTER-SALES <b>AI</b></small></span></a>
    <span className="nav-heading">WORKSPACE</span>
    <nav aria-label="Main navigation">{items.map(item => <a key={item.page} href={routeHref(item.page)} aria-current={page === item.page ? 'page' : undefined} className={`nav-link ${page === item.page ? 'active' : ''}`} onClick={() => { onClose(); if (item.page === 'assistant') onAssistant() }}><Icon name={item.icon} /><span>{item.label}</span>{page === item.page && <span className="active-indicator" />}</a>)}</nav>
    <div className="sidebar-bottom"><span className="sidebar-signal" /><div><strong>Connected workspace</strong><small>Demo tenant · Local SQL</small></div></div><button className="sidebar-collapse-button" onClick={onCollapse} aria-label="Collapse navigation" title="Collapse navigation"><Icon name="sidebar-collapse" size={18} /></button>
  </>
  return <><aside className={`sidebar ${collapsed ? 'is-collapsed' : ''}`}>{content}</aside>{mobileOpen && <Dialog title="Workspace navigation" className="nav-drawer" onClose={onClose}>{content}</Dialog>}</>
}
