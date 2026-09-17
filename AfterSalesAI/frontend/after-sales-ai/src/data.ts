export const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://127.0.0.1:5088'
export const tenantId = '00000000-0000-0000-0000-000000000001'
export const applicationId = '20000000-0000-0000-0000-000000000001'

export type Dashboard = { openOrders: number; ordersInDelivery: number; deliveryExceptions: number; openClaims: number; partsInStock: number; lowStockParts: number; recentAlerts: string[] }
export type Order = { orderId: string; customer: string; status: string; deliveryStatus: string; expectedDelivery: string; partNumber: string; alert: string; carrier?: string; trackingNumber?: string; shipmentEstimatedDelivery?: string }
export type Claim = { claimId: string; orderId: string; status: string; reason: string; createdOn: string }
export type Stock = { partNumber: string; description: string; plant: string; availableQuantity: number; reorderLevel: number }
export type AssistantResponse = { answer: string; decision: string; sources: string[] }
export type OperationsData = { dashboard: Dashboard; orders: Order[]; claims: Claim[]; inventory: Stock[] }
export type KnowledgeDocument = { id: string; title: string; category: string; sourcePath: string; content: string; summary: string }

export async function get<T>(path: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, { signal })
  if (!response.ok) throw new Error(`Unable to load application data (${response.status}). Check that the backend is running.`)
  return response.json() as Promise<T>
}

export async function loadOperations(signal?: AbortSignal): Promise<OperationsData> {
  const [dashboard, orders, claims, inventory] = await Promise.all([
    get<Dashboard>('/api/dashboard', signal), get<Order[]>('/api/orders', signal),
    get<Claim[]>('/api/claims', signal), get<Stock[]>('/api/inventory', signal),
  ])
  return { dashboard, orders, claims, inventory }
}

export async function askAssistant(message: string, signal?: AbortSignal): Promise<AssistantResponse> {
  const response = await fetch(`${API_BASE}/api/assistant/chat`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ tenantId, applicationId, message }), signal,
  })
  if (!response.ok) throw new Error(`The assistant could not complete this request (${response.status}). Please retry.`)
  return response.json() as Promise<AssistantResponse>
}

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
