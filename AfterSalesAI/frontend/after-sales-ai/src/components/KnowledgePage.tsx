import { useEffect, useState } from 'react'
import { loadKnowledgeDocuments } from '../data'
import type { KnowledgeDocument } from '../data'
import { DataTable } from './DataTable'
import { Dialog } from './Dialog'
import { Icon } from './Icon'

export function KnowledgePage({ onAsk, refreshKey }: { onAsk: (prompt: string) => void; refreshKey: number }) {
  const [selected, setSelected] = useState<KnowledgeDocument | null>(null)
  const [documents, setDocuments] = useState<KnowledgeDocument[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [retry, setRetry] = useState(0)
  useEffect(() => {
    const controller = new AbortController()
    setLoading(true); setError(''); setSelected(null)
    loadKnowledgeDocuments(controller.signal).then(result => {
      if (!controller.signal.aborted) setDocuments(result)
    }).catch((requestError: unknown) => {
      if (!controller.signal.aborted) setError(requestError instanceof Error ? requestError.message : 'Unable to load knowledge documents.')
    }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [refreshKey, retry])
  const categoryLabel = (category: string) => category.replaceAll('_', ' ')
  return <>
    <div className="info-banner"><Icon name="knowledge" /><div><strong>Workshop knowledge & SOP library</strong><p>Business processes, policies, SOPs and support guidance from Project Documents PDFs. This library shows the same indexed text used by the assistant, not API structure references or live operational records.</p></div></div>
    {loading && <div className="loading-surface" role="status"><span className="loading-ring" /><p>Loading indexed knowledge documents…</p></div>}
    {error && <div className="error-banner" role="alert"><Icon name="alert" /><div>{error}</div><button className="secondary" onClick={() => setRetry(value => value + 1)}>Retry</button></div>}
    {!loading && !error && <DataTable<KnowledgeDocument> title="Knowledge & SOP library" description="Search indexed business documents by title, topic or content" rows={documents} searchText={row => `${row.title} ${categoryLabel(row.category)} ${row.content}`}
      filters={[...new Set(documents.map(doc => doc.category))].map(category => ({ value: category, label: categoryLabel(category), matches: row => row.category === category }))}
      columns={[
        { label: 'Document', render: row => <button className="record-link" onClick={() => setSelected(row)}>{row.title}</button> },
        { label: 'Topic', render: row => <span className="location-tag">{categoryLabel(row.category)}</span> },
        { label: 'Overview', render: row => <span className="document-summary">{row.summary || 'Open document to read the guidance.'}</span> },
      ]} emptyMessage={documents.length ? 'No knowledge documents match your search.' : 'No business documents are indexed. Run knowledge ingestion for Project Documents, then refresh.'} />}
    {selected && <Dialog title={selected.title} onClose={() => setSelected(null)} className="document-dialog">
      <span className="eyebrow">{categoryLabel(selected.category)} · Business document</span><p>{selected.summary}</p>
      <p className="document-path">Source: Project Documents/{selected.sourcePath}</p>
      <button className="primary" onClick={() => { onAsk(`Explain ${selected.title} in dealer-friendly terms.`); setSelected(null) }}><Icon name="assistant" /> Ask about this topic</button>
      <details className="document-content"><summary>Read indexed PDF text</summary><pre>{selected.content}</pre></details>
    </Dialog>}
  </>
}
