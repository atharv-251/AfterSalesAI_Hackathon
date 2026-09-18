import { useEffect, useRef, useState } from 'react'
import { currentUser, formatNumber, isDeliveryException, isInDelivery, isLowStock, isOpenClaim, isOpenOrder, loadOperations, logout } from './data'
import type { CurrentUser } from './data'
import type { OperationsData } from './data'
import { pageTitles, routeHref, useNavigation } from './navigation'
import { Sidebar } from './components/Navigation'
import { KpiCard } from './components/KpiCard'
import { Icon } from './components/Icon'
import { OrdersTable, InventoryTable, ClaimsTable } from './components/OperationsTables'
import type { Detail } from './components/OperationsTables'
import { RecordDetails } from './components/Dialog'
import { KnowledgePage } from './components/KnowledgePage'
import { AssistantChat } from './components/AssistantChat'
import type { ChatDraft } from './components/AssistantChat'
import { StatusBadge } from './components/DataTable'
import { LoginScreen } from './components/LoginScreen'
import { Tenant2Operations } from './components/Tenant2Operations'

export default function App() {
  const [user, setUser] = useState<CurrentUser | null>(null)
  const [checking, setChecking] = useState(true)
  useEffect(() => { currentUser().then(setUser).finally(() => setChecking(false)) }, [])
  if (checking) return <main className="login-shell">Checking sign-in…</main>
  if (!user) return <LoginScreen onLogin={setUser} />
  const signOut = () => { void logout().finally(() => setUser(null)) }
  return user.tenantId === '00000000-0000-0000-0000-000000000001'
    ? <Tenant1Workspace user={user} onLogout={signOut} /> : <Tenant2Workspace tenantId={user.tenantId} user={user} onLogout={signOut} />
}

function Tenant2Workspace({ tenantId, user, onLogout }: { tenantId: string; user: CurrentUser; onLogout: () => void }) {
  const route = useNavigation()
  const [mobileOpen, setMobileOpen] = useState(false)
  const [navCollapsed, setNavCollapsed] = useState(false)
  const [chatOpen, setChatOpen] = useState(false)
  const [draft, setDraft] = useState<ChatDraft | null>(null)
  const ask = (text: string) => { setDraft(current => ({ text, token: (current?.token ?? 0) + 1 })); setChatOpen(true) }
  const isOperationsPage = route.page === 'dashboard' || route.page === 'service-overview' || route.page === 'repair-status' || route.page === 'warranty-summary'
  return <div className={`app-shell tenant2-workspace ${navCollapsed ? 'sidebar-collapsed' : ''}`}>
    <Sidebar page={route.page} tenant={2} collapsed={navCollapsed} mobileOpen={mobileOpen} onClose={() => setMobileOpen(false)} onCollapse={() => setNavCollapsed(true)} onAssistant={() => setChatOpen(true)} />
    <div className="workspace"><header className="topbar"><div className="topbar-context"><button className="icon-button mobile-menu" aria-label="Open navigation" aria-expanded={mobileOpen} onClick={() => setMobileOpen(true)}><Icon name="menu" /></button><button className="icon-button desktop-nav-toggle" onClick={() => setNavCollapsed(value => !value)} aria-label={navCollapsed ? 'Expand navigation' : 'Collapse navigation'} title={navCollapsed ? 'Expand navigation' : 'Collapse navigation'}><Icon name={navCollapsed ? 'sidebar-expand' : 'sidebar-collapse'} /></button><span>WORKSPACE <span className="breadcrumb-divider">/</span> <strong>{pageTitles[route.page]}</strong></span></div><div className="tenant-context"><span className="tenant-dot" /><span>{user.tenantName}</span><span className="avatar" aria-label={user.displayName}>{user.displayName.slice(0, 2).toUpperCase()}</span><button className="secondary" onClick={onLogout}>Sign out</button></div></header>
      <main className="page-content"><div className="page-heading"><div><span className="eyebrow">VEHICLE SERVICE WORKSPACE</span><h1>{pageTitles[route.page]}</h1><p>Live workshop repairs and warranty records from Tenant 2.</p></div><span className="connection-label">Tenant 2 SQL · API connected</span></div>
        {isOperationsPage && <Tenant2Operations tenantId={tenantId} page={route.page} onAsk={ask} />}
        {route.page === 'assistant' && <section className="surface assistant-home"><span className="welcome-icon"><Icon name="service" size={38} /></span><span className="eyebrow">VEHICLE SERVICE ASSISTANT</span><h2>Repair and warranty insights.</h2><p>Ask questions about live service, repair, and warranty records without leaving your workspace.</p><button className="primary" onClick={() => setChatOpen(true)}><Icon name="service" />Open AI Assistant</button></section>}
      </main><footer className="app-footer"><strong>i-mobilothon 2026</strong><span>Team Name - Smart Workshop Assistance</span></footer></div>
    <AssistantChat selectedTenantId={tenantId} botIcon="service" open={chatOpen} onOpen={() => setChatOpen(true)} onClose={() => setChatOpen(false)} draft={draft} suggestions={['Show repair status for D001.', 'Show warranty decisions for D003.', 'Show repairs for T2ONLY-001.']} />
  </div>
}

function Tenant1Workspace({ user, onLogout }: { user: CurrentUser; onLogout: () => void }) {
  const route = useNavigation()
  const [data, setData] = useState<OperationsData | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [refresh, setRefresh] = useState(0)
  const [updated, setUpdated] = useState('')
  const [mobileOpen, setMobileOpen] = useState(false)
  const [navCollapsed, setNavCollapsed] = useState(false)
  const [chatOpen, setChatOpen] = useState(false)
  const [draft, setDraft] = useState<ChatDraft | null>(null)
  const [detail, setDetail] = useState<Detail | null>(null)
  const [dashboardDataTab, setDashboardDataTab] = useState<'orders' | 'inventory'>('orders')
  const titleRef = useRef<HTMLHeadingElement>(null)
  useEffect(() => {
    const controller = new AbortController()
    setLoading(true); setError('')
    loadOperations(controller.signal).then(result => {
      if (!controller.signal.aborted) { setData(result); setUpdated(new Date().toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })) }
    }).catch((requestError: unknown) => {
      if (!controller.signal.aborted) setError(requestError instanceof Error ? requestError.message : 'Unable to load operational data.')
    }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [refresh])
  useEffect(() => {
    setMobileOpen(false)
    if (route.page === 'assistant') setChatOpen(true)
    else titleRef.current?.focus()
  }, [route.page])
  const ask = (text: string) => {
    setDraft(current => ({ text, token: (current?.token ?? 0) + 1 }))
    setChatOpen(true)
  }
  const suggestions = [
    data?.orders.length ? `Explain the status of order ${(data.orders.find(isDeliveryException) ?? data.orders[0]).orderId}.` : 'Show the dealer dashboard alerts.',
    data?.inventory.length ? `Is part ${(data.inventory.find(isLowStock) ?? data.inventory[0]).partNumber} available?` : 'How do I check parts availability?',
    data?.claims.length ? `What is the status of claim ${data.claims[0].claimId}?` : 'How do I raise a claim?',
    'Show the dealer dashboard alerts.',
  ].filter((item, index, all) => all.indexOf(item) === index)
  const tableKey = `${route.page}:${route.filter}`
  return <div className={`app-shell ${navCollapsed ? 'sidebar-collapsed' : ''}`}>
    <a className="skip-link" href="#main-content" onClick={event => { event.preventDefault(); titleRef.current?.focus() }}>Skip to main content</a>
    <Sidebar page={route.page} tenant={1} collapsed={navCollapsed} mobileOpen={mobileOpen} onClose={() => setMobileOpen(false)} onCollapse={() => setNavCollapsed(true)} onAssistant={() => setChatOpen(true)} />
    <div className="workspace">
      <header className="topbar"><div className="topbar-context"><button className="icon-button mobile-menu" aria-label="Open navigation" aria-expanded={mobileOpen} onClick={() => setMobileOpen(true)}><Icon name="menu" /></button><button className="icon-button desktop-nav-toggle" onClick={() => setNavCollapsed(value => !value)} aria-label={navCollapsed ? 'Expand navigation' : 'Collapse navigation'} title={navCollapsed ? 'Expand navigation' : 'Collapse navigation'}><Icon name={navCollapsed ? 'sidebar-expand' : 'sidebar-collapse'} /></button><span>WORKSPACE <span className="breadcrumb-divider">/</span> <strong>{route.page === 'dashboard' ? 'Dashboard' : pageTitles[route.page]}</strong></span></div><div className="tenant-context"><span className="tenant-dot" /><span>{user.tenantName}</span><span className="avatar" aria-label={user.displayName}>{user.displayName.slice(0, 2).toUpperCase()}</span><button className="secondary" onClick={onLogout}>Sign out</button></div></header>
      <main id="main-content" className="page-content">
        <div className="page-heading"><div><span className="eyebrow">SMART WORKSHOP ASSISTANCE</span><h1 ref={titleRef} tabIndex={-1}>{pageTitles[route.page]}</h1><p>After-sales intelligence. One connected workspace.</p></div><div className="header-actions"><span className={`connection-label ${error ? 'connection-error' : ''}`}>{error ? 'Connection needs attention' : loading ? 'Connecting…' : 'Local SQL · API connected'}</span><button className="secondary" onClick={() => setRefresh(value => value + 1)} disabled={loading}><Icon name="refresh" size={16} />Refresh data</button></div></div>
        {error && <div className="error-banner" role="alert"><Icon name="alert" /><div>{error}{data && <small>Showing the last successfully loaded records.</small>}</div><button className="secondary" onClick={() => setRefresh(value => value + 1)} disabled={loading}>Retry</button></div>}
        {loading && !data && <div className="loading-surface" role="status"><span className="loading-ring" /><h2>Connecting your workshop</h2><p>Loading orders, claims and inventory…</p></div>}
        {data && route.page === 'dashboard' && <>
          <section className="command-banner"><div><span className="eyebrow">OPERATIONS COMMAND CENTER</span><h2>A clearer view. A smarter next step.</h2><p>Monitor demand, track exceptions and keep your workshop moving.</p></div><a className="banner-link" href={routeHref('deliveries', 'exceptions')}>Review delivery exceptions <Icon name="arrow" /></a></section>
          <section className="metrics" aria-label="Key performance indicators">
            <KpiCard label="Open Orders" value={data.orders.filter(isOpenOrder).length} detail="Open / confirmed order lines" icon="orders" href={routeHref('orders', 'open')} />
            <KpiCard label="In Delivery" value={data.orders.filter(isInDelivery).length} detail="Order lines in transit" icon="deliveries" href={routeHref('deliveries', 'in-delivery')} />
            <KpiCard label="Delivery Exceptions" value={data.orders.filter(isDeliveryException).length} detail="Delayed, lost or exception lines" icon="alert" href={routeHref('deliveries', 'exceptions')} warning />
            <KpiCard label="Open Claims" value={data.claims.filter(isOpenClaim).length} detail="Claims awaiting completion" icon="claims" href={routeHref('claims', 'open')} />
            <KpiCard label="Parts in Stock" value={data.inventory.filter(row => row.availableQuantity > 0).reduce((sum, row) => sum + row.availableQuantity, 0)} detail="Available units across locations" icon="inventory" href={routeHref('inventory')} />
            <KpiCard label="Low Stock" value={data.inventory.filter(isLowStock).length} detail="At / below reorder level" icon="inventory" href={routeHref('inventory', 'low')} warning />
          </section>
          <div className="dashboard-grid"><div className="dashboard-main"><section className="dashboard-data-tabs" aria-label="Operational records"><div role="tablist" aria-label="Operational records"><button role="tab" aria-selected={dashboardDataTab === 'orders'} className={dashboardDataTab === 'orders' ? 'active' : ''} onClick={() => setDashboardDataTab('orders')}>Order register</button><button role="tab" aria-selected={dashboardDataTab === 'inventory'} className={dashboardDataTab === 'inventory' ? 'active' : ''} onClick={() => setDashboardDataTab('inventory')}>Parts & inventory</button></div>{dashboardDataTab === 'orders' ? <OrdersTable rows={data.orders} onDetail={setDetail} /> : <InventoryTable rows={data.inventory} onDetail={setDetail} />}</section></div><div className="dashboard-side">
            <section className="surface activity-panel"><div className="section-heading"><h2>Recent alerts</h2><span className="alert-icon"><Icon name="alert" size={18} /></span></div><p className="muted">Latest operational signals</p><ul className="activity-list">{[...new Set(data.dashboard.recentAlerts)].slice(0, 5).map(alert => <li key={alert}><span className="activity-dot" /><p>{alert}</p></li>)}</ul>{!data.dashboard.recentAlerts.length && <p className="muted">No recent alerts reported.</p>}<button className="text-button" onClick={() => ask('Show the dealer dashboard alerts.')}>Ask for an alert summary <Icon name="arrow" size={16} /></button></section>
            <section className="surface activity-panel"><div className="section-heading"><h2>Claims snapshot</h2><Icon name="claims" size={18} /></div>{data.claims.filter(isOpenClaim).slice(0, 3).map(claim => <div className="claim-preview" key={claim.claimId}><strong>{claim.claimId}</strong><StatusBadge value={claim.status} /><p>{claim.reason}</p></div>)}{!data.claims.some(isOpenClaim) && <p className="muted">No open claims.</p>}<a className="text-button" href={routeHref('claims', 'open')}>View open claims <Icon name="arrow" size={16} /></a></section>
            <section className="insight-card"><Icon name="assistant" size={28} /><h2>From data to answers</h2><p>Ask a question in plain language. Your assistant connects operational facts with reference guidance.</p><button onClick={() => setChatOpen(true)}>Open AI Assistant <Icon name="arrow" size={16} /></button></section>
          </div></div>
        </>}
        {data && route.page === 'orders' && <OrdersTable key={tableKey} rows={data.orders} filter={route.filter} onDetail={setDetail} />}
        {data && route.page === 'deliveries' && <OrdersTable key={tableKey} rows={data.orders} delivery filter={route.filter} onDetail={setDetail} />}
        {data && route.page === 'claims' && <ClaimsTable key={tableKey} rows={data.claims} filter={route.filter} onDetail={setDetail} />}
        {data && route.page === 'inventory' && <InventoryTable key={tableKey} rows={data.inventory} filter={route.filter} onDetail={setDetail} />}
        {route.page === 'knowledge' && <KnowledgePage onAsk={ask} refreshKey={refresh} />}
        {route.page === 'assistant' && <section className="surface assistant-home"><span className="welcome-icon"><Icon name="assistant" size={38} /></span><span className="eyebrow">YOUR WORKSHOP COPILOT</span><h2>Ask. Understand. Act.</h2><p>Explore operational records and reference guidance without leaving your workspace. The floating panel keeps your conversation available across pages.</p><button className="primary" onClick={() => setChatOpen(true)}><Icon name="assistant" />Open assistant</button><div className="prompt-grid">{suggestions.map(prompt => <button key={prompt} className="prompt-card" onClick={() => ask(prompt)}>{prompt}<Icon name="arrow" size={18} /></button>)}</div></section>}
        {data && <p className="data-note">{formatNumber(data.orders.length)} order lines · {formatNumber(data.inventory.length)} inventory records · Last refreshed {updated}. KPI counts reflect the displayed record filters.</p>}
      </main>
      <footer className="app-footer"><strong>i-mobilothon 2026</strong><span>Team Name - Smart Workshop Assistance</span></footer>
    </div>
    {detail && <RecordDetails title={detail.title} fields={detail.fields} onClose={() => setDetail(null)} onAsk={() => { ask(detail.prompt); setDetail(null) }} />}
    <AssistantChat open={chatOpen} onOpen={() => setChatOpen(true)} onClose={() => setChatOpen(false)} draft={draft} suggestions={suggestions} />
  </div>
}
