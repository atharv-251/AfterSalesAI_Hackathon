import { useEffect, useState } from 'react'
import { get } from '../data'
import type { Page } from '../navigation'
import { Icon } from './Icon'
import { DataTable, StatusBadge } from './DataTable'

type Repair = { repairOrderNumber: string; vehicleReference: string; modelName: string; complaint: string; status: string; priority: string; openedDate: string; promisedDate: string; closedDate?: string; estimatedAmountEur: number; isOverdue: boolean }
type Warranty = { repairOrderNumber: string; caseNumber: string; status: string; reason: string; submittedDate: string; decisionDate?: string; claimedAmountEur: number; approvedAmountEur: number }
type Result = { dealer: { dealerId: string; dealerName: string; isActive: boolean }; repairs: Repair[]; warrantyCases: Warranty[]; warrantySummary?: { totalCases: number; pendingCases: number; claimedAmountEur: number; approvedAmountEur: number } }
type Dealer = { dealerId: string; dealerName: string; isActive: boolean }

const routes = { 'service-overview': 'service-overview', 'repair-status': 'repair-status', 'warranty-summary': 'warranty-summary' } as const
const formatCurrency = (value: number) => new Intl.NumberFormat('en-US', { style: 'currency', currency: 'EUR' }).format(value)
const formatDate = (value?: string) => value ? new Date(`${value.slice(0, 10)}T12:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : 'Not recorded'

export function Tenant2Operations({ tenantId, page, onAsk }: { tenantId: string; page: Page; onAsk: (prompt: string) => void }) {
  const [dealerId, setDealerId] = useState('D001')
  const [dealers, setDealers] = useState<Dealer[]>([])
  const [results, setResults] = useState<Partial<Record<keyof typeof routes, Result>>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const load = async () => {
    const pages = page === 'dashboard' ? Object.keys(routes) as (keyof typeof routes)[] : [page as keyof typeof routes]
    setLoading(true); setError('')
    try {
      const loaded = await Promise.all(pages.map(async item => [item, await get<Result>(`/api/tenant2/dealers/${encodeURIComponent(dealerId)}/${routes[item]}?tenantId=${tenantId}`)] as const))
      setResults(Object.fromEntries(loaded))
    } catch (requestError) { setError(requestError instanceof Error ? requestError.message : 'Unable to load Tenant 2 service data.') }
    finally { setLoading(false) }
  }
  useEffect(() => {
    const controller = new AbortController()
    void get<Dealer[]>(`/api/tenant2/dealers?tenantId=${tenantId}`, controller.signal)
      .then(availableDealers => {
        setDealers(availableDealers)
        setDealerId(currentDealerId => availableDealers.some(dealer => dealer.dealerId === currentDealerId)
          ? currentDealerId
          : availableDealers[0]?.dealerId ?? currentDealerId)
      })
      .catch(requestError => {
        if (!controller.signal.aborted) setError(requestError instanceof Error ? requestError.message : 'Unable to load Tenant 2 dealers.')
      })
    return () => controller.abort()
  }, [tenantId])
  useEffect(() => { void load() }, [page])
  const service = results['service-overview']
  const repairs = results['repair-status']?.repairs ?? service?.repairs ?? []
  const warranty = results['warranty-summary']
  const overview = page === 'dashboard'
  return <>
    <section className="tenant2-command surface"><div><span className="eyebrow">TENANT 2 · VEHICLE SERVICE</span><h2>{overview ? 'Workshop service command center' : page === 'service-overview' ? 'Service activity by dealer' : page === 'repair-status' ? 'Repair progress by dealer' : 'Warranty decisions by dealer'}</h2><p>Live records are read from the Tenant 2 service database.</p></div><form onSubmit={event => { event.preventDefault(); void load() }}><label>Dealer Name<select value={dealerId} disabled={dealers.length === 0} onChange={event => setDealerId(event.target.value)}>{dealers.length === 0 ? <option>Loading dealers…</option> : dealers.map(dealer => <option key={dealer.dealerId} value={dealer.dealerId}>{dealer.dealerName}</option>)}</select></label><button className="primary" disabled={loading || dealers.length === 0}>{loading ? 'Loading…' : 'Load records'}</button></form></section>
    {error && <div className="error-banner" role="alert"><Icon name="alert" /><div>{error}</div><button className="secondary" onClick={() => void load()}>Retry</button></div>}
    {!error && loading && <div className="loading-surface" role="status"><span className="loading-ring" /><p>Loading Tenant 2 service records…</p></div>}
    {!error && !loading && <>
      {(service || warranty) && <section className="tenant2-summary"><article><span>Dealer</span><strong>{(service ?? warranty)?.dealer.dealerName}</strong><small>{dealerId} · {(service ?? warranty)?.dealer.isActive ? 'Active account' : 'Inactive account'}</small></article><article><span>Open repairs</span><strong>{repairs.filter(repair => !repair.closedDate).length}</strong><small>{repairs.filter(repair => repair.isOverdue).length} overdue</small></article><article><span>Pending warranties</span><strong>{warranty?.warrantySummary?.pendingCases ?? warranty?.warrantyCases.filter(item => !/approved|rejected/i.test(item.status)).length ?? '—'}</strong><small>{warranty?.warrantySummary ? formatCurrency(warranty.warrantySummary.claimedAmountEur) + ' claimed' : 'Load warranty summary'}</small></article></section>}
      {(overview || page === 'service-overview' || page === 'repair-status') && <DataTable<Repair> title="Repair orders" description="Vehicle service work and current repair status" rows={repairs} searchText={repair => `${repair.repairOrderNumber} ${repair.vehicleReference} ${repair.modelName} ${repair.complaint} ${repair.status} ${repair.priority}`} filters={[...new Set(repairs.map(repair => repair.status))].map(status => ({ value: status, label: status, matches: repair => repair.status === status }))} date={repair => repair.promisedDate} dateLabel="Promised date" columns={[{ label: 'Repair order', render: repair => <><strong>{repair.repairOrderNumber}</strong><small>{repair.complaint}</small></> }, { label: 'Vehicle', render: repair => <>{repair.vehicleReference}<small>{repair.modelName}</small></> }, { label: 'Status', render: repair => <StatusBadge value={repair.status} warning={repair.isOverdue} /> }, { label: 'Promised', render: repair => formatDate(repair.promisedDate) }, { label: 'Estimate', render: repair => formatCurrency(repair.estimatedAmountEur), numeric: true }]} emptyMessage="No repair orders match the selected filters." />}
      {(overview || page === 'warranty-summary') && <DataTable<Warranty> title="Warranty cases" description="Submitted claims and decision outcomes" rows={warranty?.warrantyCases ?? []} searchText={item => `${item.caseNumber} ${item.repairOrderNumber} ${item.status} ${item.reason}`} filters={[...new Set((warranty?.warrantyCases ?? []).map(item => item.status))].map(status => ({ value: status, label: status, matches: item => item.status === status }))} date={item => item.submittedDate} dateLabel="Submitted date" columns={[{ label: 'Case', render: item => <><strong>{item.caseNumber}</strong><small>{item.reason}</small></> }, { label: 'Repair order', render: item => item.repairOrderNumber }, { label: 'Status', render: item => <StatusBadge value={item.status} /> }, { label: 'Submitted', render: item => formatDate(item.submittedDate) }, { label: 'Approved', render: item => formatCurrency(item.approvedAmountEur), numeric: true }]} emptyMessage="No warranty cases match the selected filters." />}
    </>}
  </>
}
