import { useEffect, useRef, useState } from 'react'
import { get } from '../data'

export function DealerIntegrationPanel({ tenantId, wrapper }: { tenantId: string; wrapper: boolean }) {
  const [dealerId, setDealerId] = useState('D001')
  const [operation, setOperation] = useState(0)
  const [result, setResult] = useState<unknown>(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const request = useRef<AbortController | null>(null)
  useEffect(() => () => request.current?.abort(), [])
  const routes = wrapper ? ['aftersales-overview', 'repair-readiness', 'claims-warranty-summary'] : ['service-overview', 'repair-status', 'warranty-summary']
  async function load() {
    request.current?.abort()
    const controller = new AbortController()
    request.current = controller
    setBusy(true); setError(''); setResult(null)
    try {
      const path = wrapper ? 'tenant1/wrapper' : 'tenant2'
      const response = await get<unknown>(`/api/${path}/dealers/${encodeURIComponent(dealerId)}/${routes[operation]}?tenantId=${tenantId}`, controller.signal)
      if (!controller.signal.aborted) setResult(response)
    } catch (e) {
      if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Request failed.')
    } finally { if (!controller.signal.aborted) setBusy(false) }
  }
  return <section className="surface activity-panel">
    <h2>{wrapper ? 'Tenant 1 wrapper API test' : 'Tenant 2 service and warranty records'}</h2>
    <p>{wrapper ? 'Dealer-level correlation through HTTP. Source data remains separate.' : 'Only Tenant 2 vehicle servicing data is queried.'}</p>
    <form onSubmit={e => { e.preventDefault(); void load() }}>
      <label>Dealer ID <input value={dealerId} pattern="[A-Z0-9-]{1,50}" maxLength={50} required onChange={e => setDealerId(e.target.value.toUpperCase())} /></label>
      <label> Operation <select value={operation} onChange={e => setOperation(Number(e.target.value))}>{routes.map((route, i) => <option key={route} value={i}>{route}</option>)}</select></label>
      <button className="primary" disabled={busy} type="submit">{busy ? 'Loading…' : 'Run approved operation'}</button>
    </form>
    {error && <p role="alert">{error}</p>}
    {result !== null && <pre style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', maxHeight: 500, overflow: 'auto' }}>{JSON.stringify(result, null, 2)}</pre>}
  </section>
}
