import { useState } from 'react'

const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000'

const tenantId = '00000000-0000-0000-0000-000000000001'
const applicationId = '20000000-0000-0000-0000-000000000001'

type AssistantResponse = {
  answer: string
  decision: string
  sources: string[]
}

export default function App() {
  const [message, setMessage] = useState('')
  const [response, setResponse] = useState<AssistantResponse | null>(null)
  const [loading, setLoading] = useState(false)

  async function askAssistant() {
    if (!message.trim()) return

    setLoading(true)
    try {
      const res = await fetch(`${API_BASE}/api/assistant/chat`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tenantId, applicationId, message }),
      })
      if (!res.ok) throw new Error(`API request failed: ${res.status}`)
      setResponse(await res.json())
    } catch (error) {
      setResponse({
        answer: error instanceof Error ? error.message : 'Unable to reach the backend.',
        decision: 'ERROR',
        sources: [],
      })
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="shell">
      <section className="panel">
        <header className="header">
          <div>
            <div className="eyebrow">AFTER-SALES AI</div>
            <h1>Enterprise Assistant</h1>
            <p>API + Database + RAG orchestration foundation</p>
          </div>
          <span className="status">LOCAL</span>
        </header>

        <div className="conversation">
          <div className="welcome">
            <h2>Ask an after-sales question</h2>
            <p>
              This first scaffold connects the React UI to the .NET orchestration API.
              The real agent, tools, RAG and LLMaaS integration come next.
            </p>
          </div>

          {response && (
            <article className="response-card">
              <div className="response-meta">
                <span>Decision: {response.decision}</span>
                <span>{response.sources.length} sources</span>
              </div>
              <p>{response.answer}</p>
            </article>
          )}
        </div>

        <div className="composer">
          <textarea
            value={message}
            onChange={(e) => setMessage(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && !e.shiftKey) {
                e.preventDefault()
                void askAssistant()
              }
            }}
            placeholder="Example: Why is order PO-2026-00891 delayed?"
            rows={3}
          />
          <button onClick={() => void askAssistant()} disabled={loading}>
            {loading ? 'Thinking…' : 'Ask Assistant'}
          </button>
        </div>
      </section>
    </main>
  )
}
