export type RowFilter<T> = { value: string; label: string; matches: (row: T) => boolean }
export type FilterValues = { search: string; status: string; place: string; from: string; to: string }
export function filterRows<T>(rows: T[], values: FilterValues, searchText: (row: T) => string, filters: RowFilter<T>[], location?: (row: T) => string, date?: (row: T) => string) {
  const filter = filters.find(item => item.value === values.status)
  const term = values.search.trim().toLowerCase()
  return rows.map((row, index) => ({ row, index })).filter(({ row }) => (!term || searchText(row).toLowerCase().includes(term))
    && (!filter || filter.matches(row)) && (!values.place || location?.(row) === values.place)
    && (!values.from || (date?.(row) ?? '') >= values.from) && (!values.to || (date?.(row) ?? '') <= values.to))
}
export function paginate<T>(rows: T[], page: number, pageSize = 10) {
  const currentPage = Math.max(1, Math.min(page, Math.max(1, Math.ceil(rows.length / pageSize))))
  return { currentPage, rows: rows.slice((currentPage - 1) * pageSize, currentPage * pageSize) }
}
