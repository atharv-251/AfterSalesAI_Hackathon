export const API_BASE = import.meta.env.VITE_API_BASE_URL ?? ''
export const tenantId = '00000000-0000-0000-0000-000000000001'
export const applicationId = '20000000-0000-0000-0000-000000000001'

export type Dashboard = { openOrders: number; ordersInDelivery: number; deliveryExceptions: number; openClaims: number; partsInStock: number; lowStockParts: number; recentAlerts: string[] }
export type Order = { orderId: string; customer: string; status: string; deliveryStatus: string; expectedDelivery: string; partNumber: string; alert: string; carrier?: string; trackingNumber?: string; shipmentEstimatedDelivery?: string }
export type Claim = { claimId: string; orderId: string; status: string; reason: string; createdOn: string }
export type Stock = { partNumber: string; description: string; plant: string; availableQuantity: number; reorderLevel: number }
export type AssistantResponse = { answer: string; decision: string; sources: string[]; sessionId?: string }
export type OperationsData = { dashboard: Dashboard; orders: Order[]; claims: Claim[]; inventory: Stock[] }
export type KnowledgeDocument = { id: string; title: string; category: string; sourcePath: string; content: string; summary: string }

export async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, { signal, credentials: 'include' })
  if (!response.ok) throw new Error(`Unable to load application data (${response.status}). Check that the backend is running.`)
  return response.json() as Promise<T>
}

export async function loadOperations(signal?: AbortSignal): Promise<OperationsData> {
  const [dashboard, orders, claims, inventory] = await Promise.all([
    get<Dashboard>(`/api/dashboard?tenantId=${tenantId}`, signal), get<Order[]>(`/api/orders?tenantId=${tenantId}`, signal),
    get<Claim[]>(`/api/claims?tenantId=${tenantId}`, signal), get<Stock[]>(`/api/inventory?tenantId=${tenantId}`, signal),
  ])
  return { dashboard, orders, claims, inventory }
}

export async function askAssistant(message: string, signal?: AbortSignal, selectedTenantId = tenantId, sessionId?: string): Promise<AssistantResponse> {
  const response = await fetch(`${API_BASE}/api/assistant/chat`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ tenantId: selectedTenantId, applicationId: selectedTenantId === tenantId ? applicationId : '20000000-0000-0000-0000-000000000002', message, sessionId }), signal, credentials: 'include',
  })
  if (!response.ok) throw new Error(`The assistant could not complete this request (${response.status}). Please retry.`)
  return response.json() as Promise<AssistantResponse>
}

export type CurrentUser = { userName: string; displayName: string; tenantId: string; tenantName: string; productName: string }
export async function login(userName: string, password: string): Promise<CurrentUser> {
  const response = await fetch(`${API_BASE}/api/auth/login`, { method: 'POST', credentials: 'include', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName, password }) })
  if (!response.ok) throw new Error('Login failed')
  return response.json() as Promise<CurrentUser>
}
export async function currentUser(): Promise<CurrentUser | null> {
  const response = await fetch(`${API_BASE}/api/auth/me`, { credentials: 'include' })
  return response.ok ? response.json() as Promise<CurrentUser> : null
}
export async function logout(): Promise<void> { await fetch(`${API_BASE}/api/auth/logout`, { method: 'POST', credentials: 'include' }) }

export async function loadKnowledgeDocuments(signal?: AbortSignal): Promise<KnowledgeDocument[]> {
  return get<KnowledgeDocument[]>(`/api/knowledge/documents?tenantId=${tenantId}&applicationId=${applicationId}`, signal)
}

export const isOpenOrder = (order: Order) => /^(open|confirmed)$/i.test(order.status)
export const isInDelivery = (order: Order) => /^(in transit|in delivery|shipped)$/i.test(order.deliveryStatus)
export const isDeliveryException = (order: Order) => /delay|lost|exception|failed|damage/i.test(order.deliveryStatus)
export const isOpenClaim = (claim: Claim) => !/^(closed|resolved|rejected|approved|completed)$/i.test(claim.status)
export const isLowStock = (stock: Stock) => stock.availableQuantity <= stock.reorderLevel
export const formatNumber = (value: number) => value.toLocaleString('en-US')
export const formatDate = (value?: string) => value ? new Date(`${value.slice(0, 10)}T12:00:00`).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : 'Not recorded'
