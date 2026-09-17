import { useId, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { Icon } from './Icon'

export type Column<T> = { label: string; render: (row: T) => ReactNode; numeric?: boolean }
export type TableFilter<T> = { value: string; label: string; matches: (row: T) => boolean }
export type TableProps<T> = {
  title: string; description?: string; rows: T[]; columns: Column<T>[]
  searchText: (row: T) => string; filters?: TableFilter<T>[]; initialFilter?: string
  location?: (row: T) => string; date?: (row: T) => string; dateLabel?: string
  emptyMessage?: string
}

export function StatusBadge({ value, warning = false }: { value: string; warning?: boolean }) {
  const tone = warning || /delay|lost|exception|reject|low|out of stock|backorder/i.test(value)
    ? 'warning' : /deliver|complet|available|approved|resolved/i.test(value) && !/not/i.test(value) ? 'success' : 'neutral'
  return <span className={`status-badge ${tone}`}><span className="status-dot" />{value}</span>
}

export function Pagination({ page, total, pageSize = 10, onChange }: { page: number; total: number; pageSize?: number; onChange: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  const start = Math.max(1, Math.min(page - 2, pages - 4))
  const numbers = Array.from({ length: Math.min(5, pages) }, (_, i) => start + i)
  return <div className="pagination">
    <span aria-live="polite">Showing <strong>{total ? (page - 1) * pageSize + 1 : 0}–{Math.min(page * pageSize, total)}</strong> of <strong>{total}</strong></span>
    <nav aria-label="Table pagination">
      <button className="page-button" disabled={page === 1} onClick={() => onChange(page - 1)} aria-label="Previous page">Previous</button>
      {numbers.map(number => <button key={number} className={`page-button page-number ${number === page ? 'selected' : ''}`} aria-label={`Page ${number}`} aria-current={number === page ? 'page' : undefined} onClick={() => onChange(number)}>{number}</button>)}
      <span className="page-summary">Page {page} of {pages}</span>
      <button className="page-button" disabled={page === pages} onClick={() => onChange(page + 1)} aria-label="Next page">Next</button>
    </nav>
  </div>
}

function Filters({ search, status, place, from, to, setValue, filters, locations, dateLabel, clear }: {
  search: string; status: string; place: string; from: string; to: string
  setValue: (field: 'search' | 'status' | 'place' | 'from' | 'to', value: string) => void
  filters: { value: string; label: string }[]; locations: string[]; dateLabel?: string; clear: () => void
}) {
  return <div className="filter-toolbar" role="search" aria-label="Filter records">
    <label className="search-control"><Icon name="search" /><span className="sr-only">Search records</span><input type="search" value={search} placeholder="Search records…" onChange={event => setValue('search', event.target.value)} /></label>
    {filters.length > 0 && <label><span className="sr-only">Status filter</span><select aria-label="Status filter" value={status} onChange={event => setValue('status', event.target.value)}><option value="">All statuses</option>{filters.map(filter => <option key={filter.value} value={filter.value}>{filter.label}</option>)}</select></label>}
    {locations.length > 0 && <label><span className="sr-only">Location filter</span><select aria-label="Location filter" value={place} onChange={event => setValue('place', event.target.value)}><option value="">All locations</option>{locations.map(item => <option key={item}>{item}</option>)}</select></label>}
    {dateLabel && <div className="date-filters"><label>{dateLabel} from<input type="date" value={from} max={to || undefined} onChange={event => setValue('from', event.target.value)} /></label><label>To<input type="date" value={to} min={from || undefined} onChange={event => setValue('to', event.target.value)} /></label></div>}
    <button className="text-button" onClick={clear} disabled={!search && !status && !place && !from && !to}>Clear filters</button>
  </div>
}

export function DataTable<T>({ title, description, rows, columns, searchText, filters = [], initialFilter = '', location, date, dateLabel, emptyMessage = 'No records match these filters.' }: TableProps<T>) {
  const id = useId()
  const [values, setValues] = useState({ search: '', status: initialFilter, place: '', from: '', to: '' })
  const [page, setPage] = useState(1)
  const setValue = (field: keyof typeof values, value: string) => { setValues(current => ({ ...current, [field]: value })); setPage(1) }
  const filtered = useMemo(() => {
    const filter = filters.find(item => item.value === values.status)
    const term = values.search.trim().toLowerCase()
    return rows.map((row, index) => ({ row, index })).filter(({ row }) => (!term || searchText(row).toLowerCase().includes(term))
      && (!filter || filter.matches(row)) && (!values.place || location?.(row) === values.place)
      && (!values.from || (date?.(row) ?? '') >= values.from) && (!values.to || (date?.(row) ?? '') <= values.to))
  }, [rows, values, filters, searchText, location, date])
  const currentPage = Math.min(page, Math.max(1, Math.ceil(filtered.length / 10)))
  const locations = location ? [...new Set(rows.map(location))].filter(Boolean).sort() : []
  return <section className="surface data-section" aria-labelledby={id}>
    <div className="section-heading"><div><h2 id={id}>{title}</h2>{description && <p>{description}</p>}</div><span className="count-label">{filtered.length} records</span></div>
    <Filters {...values} setValue={setValue} filters={filters} locations={locations} dateLabel={date ? dateLabel ?? 'Date' : undefined} clear={() => { setValues({ search: '', status: '', place: '', from: '', to: '' }); setPage(1) }} />
    <div className="table-scroll" tabIndex={0} role="region" aria-label={`${title} table`}>
      <table><caption className="sr-only">{title} — page {currentPage}</caption><thead><tr>{columns.map(column => <th key={column.label} scope="col" className={column.numeric ? 'numeric' : ''}>{column.label}</th>)}</tr></thead>
        <tbody>{filtered.slice((currentPage - 1) * 10, currentPage * 10).map(({ row, index }) => <tr key={index}>{columns.map(column => <td key={column.label} className={column.numeric ? 'numeric' : ''}>{column.render(row)}</td>)}</tr>)}</tbody>
      </table>
      {!filtered.length && <div className="empty-state"><Icon name="search" size={30} /><h3>No matching records</h3><p>{emptyMessage}</p></div>}
    </div>
    <Pagination page={currentPage} total={filtered.length} onChange={setPage} />
  </section>
}
