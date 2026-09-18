import { useEffect, useState } from 'react'
import { API_BASE, get } from '../data'

type Operation = { operationId: string; operationName: string; description: string; kind: string; relativeUrl: string; isActive: boolean }
type Document = { documentId: string; fileName: string; fileType: string }
type Session = { sessionId: string; createdDate: string }
type Message = { messageId: string; role: string; content: string }

export function CoreManagementPanel({ tenantId }: { tenantId: string }) {
  const [tab, setTab] = useState('tenant')
  const [prompt, setPrompt] = useState('')
  const [operations, setOperations] = useState<Operation[]>([])
  const [documents, setDocuments] = useState<Document[]>([])
  const [sessions, setSessions] = useState<Session[]>([])
  const [messages, setMessages] = useState<Message[]>([])
  const [content, setContent] = useState('')
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [refresh, setRefresh] = useState(0)
  const [busy, setBusy] = useState(false)
  const [file, setFile] = useState<File | null>(null)
  const scope = `tenantId=${encodeURIComponent(tenantId)}`
  useEffect(() => {
    const controller = new AbortController()
    setError(''); setContent(''); setMessages([])
    Promise.all([
      get<{ tenantSystemPrompt: string }>(`/api/core/tenant?${scope}`, controller.signal),
      get<Operation[]>(`/api/core/operations?${scope}`, controller.signal),
      get<Session[]>(`/api/core/sessions?${scope}`, controller.signal),
    ]).then(([tenant, tools, history]) => { if (!controller.signal.aborted) { setPrompt(tenant.tenantSystemPrompt); setOperations(tools); setSessions(history) } })
      .catch(e => { if (!controller.signal.aborted) setError(e instanceof Error ? e.message : 'Unable to load tenant configuration.') })
    get<Document[]>(`/api/core/documents?${scope}`, controller.signal).then(d => { if (!controller.signal.aborted) setDocuments(d) })
      .catch(() => { if (!controller.signal.aborted) setDocuments([]) })
    return () => controller.abort()
  }, [tenantId, refresh, tab])
  async function mutate(path: string, method: string, body?: BodyInit, json = true) {
    setBusy(true); setError(''); setNotice('')
    try {
      const response = await fetch(`${API_BASE}${path}?${scope}`, { method, body, credentials: 'include', headers: json ? { 'Content-Type': 'application/json' } : undefined })
      if (!response.ok) throw new Error(`Request failed (${response.status}). Check tenant ownership, file type and size.`)
      setNotice('Saved.'); setRefresh(x => x + 1)
    } catch (e) { setError(e instanceof Error ? e.message : 'Request failed.') }
    finally { setBusy(false) }
  }
  async function viewDocument(id: string) {
    try { setContent((await get<{ extractedText: string }>(`/api/core/documents/${id}?${scope}`)).extractedText) }
    catch { setError('Document is unavailable for this tenant.') }
  }
  async function viewSession(id: string) {
    try { setMessages(await get<Message[]>(`/api/core/sessions/${id}/messages?${scope}`)) }
    catch { setError('Session is unavailable for this tenant.') }
  }
  return <details className="surface activity-panel">
    <summary>Tenant configuration, documents and chat history</summary>
    <nav aria-label="Tenant management">{['tenant', 'documents', 'apis', 'database', 'history'].map(name => <button key={name} className="secondary" onClick={() => setTab(name)}>{name}</button>)}<button className="secondary" onClick={() => setRefresh(x => x + 1)}>Refresh</button></nav>
    {error && <p role="alert">{error}</p>}{notice && <p role="status">{notice}</p>}
    {tab === 'tenant' && <form onSubmit={e => { e.preventDefault(); void mutate('/api/core/tenant', 'PUT', JSON.stringify({ systemPrompt: prompt })) }}>
      <label>Tenant system prompt<textarea value={prompt} maxLength={4000} rows={5} style={{ width: '100%' }} onChange={e => setPrompt(e.target.value)} /></label>
      <p>Prompts do not grant tools, SQL access or cross-tenant permissions.</p><button disabled={busy || !prompt.trim()} className="primary">Save prompt</button>
    </form>}
    {tab === 'documents' && <>
      <form onSubmit={e => { e.preventDefault(); if (file) { const data = new FormData(); data.append('file', file); void mutate('/api/core/documents', 'POST', data, false) } }}>
        <label>Upload document (5 MB maximum)<input type="file" accept=".pdf,.docx,.txt,.md" onChange={e => setFile(e.target.files?.[0] ?? null)} /></label><button disabled={busy || !file} className="primary">Upload</button>
      </form>
      <ul>{documents.map(d => <li key={d.documentId}><button className="text-button" onClick={() => void viewDocument(d.documentId)}>{d.fileName}</button> ({d.fileType}) <button disabled={busy} onClick={() => void mutate(`/api/core/documents/${d.documentId}`, 'DELETE')}>Deactivate</button></li>)}</ul>
      {content && <pre style={{ whiteSpace: 'pre-wrap', maxHeight: 350, overflow: 'auto' }}>{content}</pre>}
    </>}
    {(tab === 'apis' || tab === 'database') && <>
      <p>Only compiled, approved operations can execute. URLs, credentials and SQL cannot be supplied here. Database operations use fixed queries; API operations use fixed routes.</p>
      <ul>{operations.filter(o => tab === 'apis' ? o.kind === 'Api' || o.kind === 'Wrapper' : o.kind === 'Database' || o.kind === 'Document').map(o => <li key={o.operationId}><strong>{o.operationName}</strong> — {o.description} <code>{o.relativeUrl}</code><label> Enabled <input type="checkbox" checked={o.isActive} disabled={busy} onChange={e => void mutate(`/api/core/operations/${o.operationId}`, 'PUT', JSON.stringify({ isActive: e.target.checked }))} /></label></li>)}</ul>
    </>}
    {tab === 'history' && <><p>Saved sessions for the selected tenant. Refresh after chatting.</p><ul>{sessions.map(s => <li key={s.sessionId}><button onClick={() => void viewSession(s.sessionId)}>{new Date(s.createdDate).toLocaleString()} — {s.sessionId.slice(0, 8)}</button></li>)}</ul>{messages.map(m => <article key={m.messageId}><strong>{m.role}</strong><pre style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{m.content}</pre></article>)}</>}
  </details>
}
