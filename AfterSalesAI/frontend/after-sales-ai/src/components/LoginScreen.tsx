import { useState } from 'react'
import { login } from '../data'

type User = { userName: string; displayName: string; tenantId: string; tenantName: string; productName: string }

export function LoginScreen({ onLogin }: { onLogin: (user: User) => void }) {
  const [userName, setUserName] = useState('tenant1.demo')
  const [password, setPassword] = useState('Tenant1Demo!')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  async function submit(event: React.FormEvent) {
    event.preventDefault(); setBusy(true); setError('')
    try { onLogin(await login(userName, password)) }
    catch { setError('Sign-in failed. Check the demo username and password.') }
    finally { setBusy(false) }
  }
  return <main className="login-shell"><section className="login-card">
    <span className="eyebrow">AFTER-SALES AI</span><h1>Sign in to your workspace</h1>
    <p>Your account determines which tenant data, documents, operations and chat history are available.</p>
    <form onSubmit={submit}><label>Username<input autoComplete="username" value={userName} onChange={e => setUserName(e.target.value)} required maxLength={100} /></label>
      <label>Password<input type="password" autoComplete="current-password" value={password} onChange={e => setPassword(e.target.value)} required maxLength={200} /></label>
      {error && <p className="login-error" role="alert">{error}</p>}<button className="primary" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button></form>
    <details><summary>Demo accounts</summary><p><strong>Tenant 1</strong>: tenant1.demo / Tenant1Demo!</p><p><strong>Tenant 2</strong>: tenant2.demo / Tenant2Demo!</p></details>
  </section></main>
}
