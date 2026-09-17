import { DataTable, StatusBadge } from './DataTable'
import type { TableFilter } from './DataTable'
import { formatDate, formatNumber, isDeliveryException, isInDelivery, isLowStock, isOpenClaim, isOpenOrder } from '../data'
import type { Claim, Order, Stock } from '../data'

export type Detail = { title: string; fields: [string, string][]; prompt: string }
const statusFilters = <T,>(rows: T[], getStatus: (row: T) => string): TableFilter<T>[] => [...new Set(rows.map(getStatus))].sort().map(status => ({ value: status, label: status, matches: row => getStatus(row) === status }))

export function OrdersTable({ rows, delivery = false, filter = '', onDetail }: { rows: Order[]; delivery?: boolean; filter?: string; onDetail: (detail: Detail) => void }) {
  const show = (row: Order) => onDetail({ title: row.orderId, prompt: `Explain the current status of order ${row.orderId}.`, fields: [
    ['Purchase order', row.orderId], ['Customer', row.customer], ['Part', row.partNumber], ['Order status', row.status],
    ['Delivery status', row.deliveryStatus], ['Requested delivery', formatDate(row.expectedDelivery)], ['Carrier', row.carrier ?? ''],
    ['Tracking number', row.trackingNumber ?? ''], ['Carrier estimate', formatDate(row.shipmentEstimatedDelivery)], ['Shipment update', row.alert],
  ] })
  const filters: TableFilter<Order>[] = delivery ? [
    { value: 'in-delivery', label: 'In delivery', matches: isInDelivery },
    { value: 'exceptions', label: 'Delivery exceptions', matches: isDeliveryException },
    ...statusFilters(rows, row => row.deliveryStatus),
  ] : [{ value: 'open', label: 'Open orders', matches: isOpenOrder }, ...statusFilters(rows, row => row.status)]
  return <DataTable title={delivery ? 'Delivery monitor' : 'Order register'} description={delivery ? 'Shipment status by purchase-order line · Select an order to inspect tracking details' : 'All purchase-order lines · Select an order number for details'} rows={rows} initialFilter={filter} filters={filters}
    searchText={row => `${row.orderId} ${row.customer} ${row.partNumber} ${row.status} ${row.deliveryStatus} ${row.carrier ?? ''} ${row.trackingNumber ?? ''}`}
    date={row => row.expectedDelivery} dateLabel="Requested delivery"
    columns={delivery ? [
      { label: 'Purchase order', render: row => <button className="record-link" onClick={() => show(row)}>{row.orderId}</button> },
      { label: 'Customer / Part', render: row => <><span className="cell-title">{row.customer}</span><small>{row.partNumber}</small></> },
      { label: 'Delivery', render: row => <StatusBadge value={row.deliveryStatus} /> },
      { label: 'Carrier', render: row => row.carrier || 'Not assigned' },
      { label: 'Tracking', render: row => <span className="mono">{row.trackingNumber || '—'}</span> },
      { label: 'Carrier estimate', render: row => formatDate(row.shipmentEstimatedDelivery) },
    ] : [
      { label: 'Order', render: row => <button className="record-link" onClick={() => show(row)}>{row.orderId}</button> },
      { label: 'Customer', render: row => row.customer },
      { label: 'Status', render: row => <StatusBadge value={row.status} /> },
      { label: 'Delivery', render: row => <StatusBadge value={row.deliveryStatus} /> },
      { label: 'Part', render: row => <span className="mono">{row.partNumber}</span> },
      { label: 'Requested delivery', render: row => formatDate(row.expectedDelivery) },
    ]} />
}

export function InventoryTable({ rows, filter = '', onDetail }: { rows: Stock[]; filter?: string; onDetail: (detail: Detail) => void }) {
  return <DataTable title="Parts & inventory" description="Availability by warehouse · Quantities and reorder levels from the inventory API" rows={rows} initialFilter={filter}
    searchText={row => `${row.partNumber} ${row.description} ${row.plant}`} location={row => row.plant}
    filters={[
      { value: 'low', label: 'Low stock', matches: isLowStock },
      { value: 'available', label: 'In stock', matches: row => row.availableQuantity > 0 },
      { value: 'unavailable', label: 'Out of stock', matches: row => row.availableQuantity <= 0 },
    ]} columns={[
      { label: 'Part', render: row => <button className="record-link" onClick={() => onDetail({ title: row.partNumber, prompt: `Is part ${row.partNumber} available?`, fields: [['Part number', row.partNumber], ['Description', row.description], ['Warehouse', row.plant], ['Available quantity', String(row.availableQuantity)], ['Reorder level', String(row.reorderLevel)]] })}>{row.partNumber}</button> },
      { label: 'Description', render: row => row.description },
      { label: 'Plant / warehouse', render: row => <span className="location-tag">{row.plant}</span> },
      { label: 'Available', numeric: true, render: row => <strong className={isLowStock(row) ? 'text-warning' : ''}>{formatNumber(row.availableQuantity)}</strong> },
      { label: 'Reorder level', numeric: true, render: row => formatNumber(row.reorderLevel) },
      { label: 'Stock status', render: row => <StatusBadge value={row.availableQuantity <= 0 ? 'Out of stock' : isLowStock(row) ? 'Low stock' : 'Available'} /> },
    ]} />
}

export function ClaimsTable({ rows, filter = '', onDetail }: { rows: Claim[]; filter?: string; onDetail: (detail: Detail) => void }) {
  return <DataTable title="Claims register" description="Track claim status and reported reasons" rows={rows} initialFilter={filter} searchText={row => `${row.claimId} ${row.orderId} ${row.status} ${row.reason}`}
    date={row => row.createdOn} dateLabel="Created" filters={[{ value: 'open', label: 'Open claims', matches: isOpenClaim }, ...statusFilters(rows, row => row.status)]}
    columns={[
      { label: 'Claim', render: row => <button className="record-link" onClick={() => onDetail({ title: row.claimId, prompt: `What is the status of claim ${row.claimId}?`, fields: [['Claim ID', row.claimId], ['Purchase order', row.orderId], ['Status', row.status], ['Reported reason', row.reason], ['Created', formatDate(row.createdOn)]] })}>{row.claimId}</button> },
      { label: 'Purchase order', render: row => <span className="mono">{row.orderId}</span> },
      { label: 'Status', render: row => <StatusBadge value={row.status} /> },
      { label: 'Reported reason', render: row => row.reason },
      { label: 'Created', render: row => formatDate(row.createdOn) },
    ]} />
}
