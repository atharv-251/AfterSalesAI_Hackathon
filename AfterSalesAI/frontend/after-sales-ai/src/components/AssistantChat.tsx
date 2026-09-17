import { useEffect, useRef, useState } from 'react'
import { askAssistant } from '../data'
import type { AssistantResponse } from '../data'
import { Icon } from './Icon'

type Message = { id: number; role: 'user' | 'assistant'; text: string; response?: AssistantResponse; failed?: boolean; question?: string }
export type ChatDraft = { text: string; token: number }
export function AnswerText({ text }: { text: string }) {
  return <>{text.split(/\n\s*\n/).map((paragraph, i) => <p key={i}>{paragraph.split(/(\*\*[^*]+\*\*|\b(?:Rejected|Delayed|Open|Delivered|Confirmed)\b)/g).map((part, j) => {
    if (part.startsWith('**')) return <strong key={j}>{part.slice(2, -2)}</strong>
    const status = part.toLowerCase()
    return ['rejected', 'delayed', 'open', 'delivered', 'confirmed'].includes(status) ? <span key={j} className={`answer-status ${status}`}>{part}</span> : part
  })}</p>)}</>
}

export function AssistantChat({ open, onOpen, onClose, draft, suggestions }: { open: boolean; onOpen: () => void; onClose: () => void; draft: ChatDraft | null; suggestions: string[] }) {
  const [input, setInput] = useState('')
  const [messages, setMessages] = useState<Message[]>([])
  const [busy, setBusy] = useState(false)
  const busyRef = useRef(false)
  const sequence = useRef(0)
  const inputRef = useRef<HTMLTextAreaElement>(null)
  const launcherRef = useRef<HTMLButtonElement>(null)
  const endRef = useRef<HTMLDivElement>(null)
  const controllerRef = useRef<AbortController | null>(null)
  useEffect(() => () => controllerRef.current?.abort(), [])
  useEffect(() => { if (draft) setInput(draft.text) }, [draft])
  useEffect(() => { if (open) inputRef.current?.focus() }, [open, draft])
  useEffect(() => { if (open) endRef.current?.scrollIntoView({ block: 'nearest' }) }, [messages, busy, open])
  const close = () => { onClose(); launcherRef.current?.focus() }

  async function send(question = input, retryId?: number) {
    const text = question.trim()
    if (!text || busyRef.current) return
    busyRef.current = true
    setBusy(true)
    setInput('')
    if (retryId !== undefined) setMessages(current => current.filter(message => message.id !== retryId))
    else setMessages(current => [...current, { id: ++sequence.current, role: 'user', text }])
    const controller = new AbortController()
    controllerRef.current = controller
    const timeout = window.setTimeout(() => controller.abort(), 90000)
    try {
      const response = await askAssistant(text, controller.signal)
      setMessages(current => [...current, { id: ++sequence.current, role: 'assistant', text: response.answer, response }])
    } catch (error) {
      setMessages(current => [...current, { id: ++sequence.current, role: 'assistant', failed: true, question: text,
        text: controller.signal.aborted ? 'This request took too long. Please try again.' : error instanceof Error ? error.message : 'Unable to reach the assistant. Please retry.' }])
    } finally {
      clearTimeout(timeout)
      busyRef.current = false
      setBusy(false)
      inputRef.current?.focus()
    }
  }

  return <>
    {open && <section className="chat-panel" role="dialog" aria-modal="false" aria-labelledby="chat-title" onKeyDown={event => { if (event.key === 'Escape') { event.stopPropagation(); close() } }}>
      <header className="chat-header"><span className="chat-avatar"><Icon name="assistant" size={24} /></span><div><h2 id="chat-title">After-Sales AI</h2><span>Smart Workshop Assistant</span></div><button className="icon-button" onClick={close} aria-label="Minimize assistant"><Icon name="close" /></button></header>
      <div className="chat-conversation" role="log" aria-label="Assistant conversation" aria-live="polite" aria-relevant="additions text">
        {!messages.length && <div className="chat-welcome"><span className="welcome-icon"><Icon name="assistant" size={30} /></span><h3>Your operations, in focus.</h3><p>Ask about an order, part, claim, or workshop alert. Answers use the existing operational data and knowledge service.</p></div>}
        {messages.map(message => <article key={message.id} className={`chat-message ${message.role} ${message.failed ? 'failed' : ''}`}><span className="message-label">{message.role === 'user' ? 'You' : 'After-Sales AI'}</span><AnswerText text={message.text} />
          {message.response && <div className="message-sources"><strong>Information fetched from</strong>{message.response.sources.length ? <ul>{message.response.sources.map((source, i) => <li key={`${source}-${i}`}>{source}</li>)}</ul> : <span>No matching source</span>}</div>}
          {message.failed && <button className="text-button" disabled={busy} onClick={() => void send(message.question, message.id)}><Icon name="refresh" size={14} />Retry request</button>}
        </article>)}
        {busy && <div className="chat-loading" role="status"><span className="typing"><i /><i /><i /></span>Analyzing workshop data…</div>}
        <div ref={endRef} />
      </div>
      {!messages.length && <div className="suggestion-chips" aria-label="Suggested questions">{suggestions.map(prompt => <button key={prompt} disabled={busy} onClick={() => void send(prompt)}>{prompt}<Icon name="arrow" size={14} /></button>)}</div>}
      <form className="chat-form" onSubmit={event => { event.preventDefault(); void send() }}>
        <label htmlFor="chat-prompt" className="sr-only">Ask the assistant</label>
        <textarea id="chat-prompt" ref={inputRef} value={input} rows={2} maxLength={4000} placeholder="Ask about your workshop…" onChange={event => setInput(event.target.value)} onKeyDown={event => { if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) { event.preventDefault(); void send() } }} />
        <button className="primary send-button" type="submit" disabled={busy || !input.trim()} aria-label="Send message"><Icon name="send" /></button>
        <small>Enter to send · Shift+Enter for a new line</small>
      </form>
    </section>}
    <button ref={launcherRef} className={`assistant-launcher ${open ? 'is-open' : ''}`} onClick={open ? close : onOpen} aria-expanded={open} aria-label={open ? 'Minimize AI Assistant' : 'Open AI Assistant'} title="AI Assistant"><Icon name="assistant" size={23} /><span>AI Assistant</span>{busy && <span className="launcher-busy" />}</button>
  </>
}
