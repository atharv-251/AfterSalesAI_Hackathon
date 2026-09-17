import { useEffect, useState } from 'react'

export const pages = ['dashboard', 'orders', 'deliveries', 'claims', 'inventory', 'knowledge', 'assistant'] as const
export type Page = typeof pages[number]
export const pageTitles: Record<Page, string> = {
  dashboard: 'Operations overview', orders: 'Orders', deliveries: 'Deliveries', claims: 'Claims',
  inventory: 'Parts & inventory', knowledge: 'Knowledge & SOP', assistant: 'AI Assistant',
}
export type Route = { page: Page; filter: string }
export function readRoute(): Route {
  const [path, query = ''] = window.location.hash.replace(/^#\/?/, '').split('?')
  return { page: pages.includes(path as Page) ? path as Page : 'dashboard', filter: new URLSearchParams(query).get('filter') ?? '' }
}
export function routeHref(page: Page, filter = '') { return `#/${page}${filter ? `?filter=${encodeURIComponent(filter)}` : ''}` }
export function useNavigation() {
  const [route, setRoute] = useState(readRoute)
  useEffect(() => {
    const onChange = () => { setRoute(readRoute()); window.scrollTo({ top: 0 }) }
    window.addEventListener('hashchange', onChange)
    return () => window.removeEventListener('hashchange', onChange)
  }, [])
  return route
}
