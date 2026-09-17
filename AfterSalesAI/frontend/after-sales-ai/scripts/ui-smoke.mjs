import assert from 'node:assert/strict'
import { spawn } from 'node:child_process'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

// Runs against real local services using Edge's DevTools protocol; no test dependencies or fixture data.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const artifacts = path.join(root, 'node_modules', '.cache', 'ui-smoke')
mkdirSync(artifacts, { recursive: true })
const browserPath = process.env.EDGE_PATH ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe'
assert.ok(existsSync(browserPath), 'Set EDGE_PATH to an installed Chromium/Edge executable')
const profile = mkdtempSync(path.join(artifacts, 'profile-'))
const browser = spawn(browserPath, ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', '--remote-debugging-port=0', `--user-data-dir=${profile}`, 'about:blank'], { stdio: ['ignore', 'ignore', 'pipe'] })
let browserDiagnostics = ''
browser.stderr.on('data', chunk => { browserDiagnostics += chunk.toString() })
const delay = ms => new Promise(resolve => setTimeout(resolve, ms))
let socket
let commandId = 0
const pending = new Map()
const exceptions = []
const send = (method, params = {}) => new Promise((resolve, reject) => {
  const id = ++commandId
  const timer = setTimeout(() => { pending.delete(id); reject(new Error(`DevTools timed out: ${method}`)) }, 110000)
  pending.set(id, { resolve, reject, timer })
  socket.send(JSON.stringify({ id, method, params }))
})
async function evaluate(expression) {
  const response = await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true, userGesture: true })
  if (response.exceptionDetails) throw new Error(response.exceptionDetails.text + ': ' + response.exceptionDetails.exception?.description)
  return response.result.value
}
async function until(expression, timeout = 15000) {
  const start = Date.now()
  while (Date.now() - start < timeout) { if (await evaluate(expression)) return; await delay(100) }
  throw new Error(`Timed out waiting for ${expression}`)
}
async function click(selector) {
  await evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); if (!el) throw Error('Missing element: ' + ${JSON.stringify(selector)}); el.click() })()`)
  await delay(150)
}
async function fill(selector, value) {
  await evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); const prototype = el.tagName === 'SELECT' ? HTMLSelectElement.prototype : el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(prototype, 'value').set.call(el, ${JSON.stringify(value)}); el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); })()`)
  await delay(150)
}
async function route(page) {
  await evaluate(`location.hash = ${JSON.stringify('#/' + page)}`)
  await delay(250)
}
async function check(name, action) { await action(); console.log(`PASS ${name}`) }
try {
  const portFile = path.join(profile, 'DevToolsActivePort')
  for (let i = 0; i < 300 && !existsSync(portFile); i++) await delay(100)
  assert.ok(existsSync(portFile), `Browser did not start: ${browserDiagnostics}`)
  const port = readFileSync(portFile, 'utf8').split('\n')[0]
  const targets = await fetch(`http://localhost:${port}/json`).then(response => response.json())
  socket = new WebSocket(targets.find(target => target.type === 'page').webSocketDebuggerUrl)
  await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }) })
  socket.addEventListener('message', event => {
    const message = JSON.parse(event.data)
    if (message.method === 'Runtime.exceptionThrown') exceptions.push(message.params.exceptionDetails)
    const request = pending.get(message.id)
    if (request) { clearTimeout(request.timer); pending.delete(message.id); message.error ? request.reject(new Error(message.error.message)) : request.resolve(message.result) }
  })
  await send('Page.enable'); await send('Runtime.enable'); await send('Network.enable')
  await send('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false })
  await send('Page.navigate', { url: process.env.UI_URL ?? 'http://localhost:5173' })
  await until('document.querySelectorAll(".kpi-card").length === 6', 30000)
  await check('dashboard contains live KPI cards and exactly ten rows per grid', async () => {
    assert.deepEqual(await evaluate('[...document.querySelectorAll("table tbody")].map(el => el.rows.length)'), [10, 10])
    assert.ok(await evaluate('document.body.innerText.includes("Team Name - Smart Workshop Assistance")'))
    assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'), true)
  })
  const desktop = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false })
  writeFileSync(path.join(artifacts, 'dashboard-desktop.png'), Buffer.from(desktop.data, 'base64'))
  for (const [label, destination, filter] of [['Open Orders', 'orders', 'open'], ['In Delivery', 'deliveries', 'in-delivery'], ['Delivery Exceptions', 'deliveries', 'exceptions'], ['Open Claims', 'claims', 'open'], ['Parts in Stock', 'inventory', ''], ['Low Stock', 'inventory', 'low']]) {
    await check(`${label} KPI opens its matching route and filter`, async () => {
      await route('dashboard')
      const count = await evaluate(`Number([...document.querySelectorAll('.kpi-card')].find(el => el.textContent.includes(${JSON.stringify(label)})).querySelector('.kpi-value').textContent.replaceAll(',', ''))`)
      await evaluate(`[...document.querySelectorAll('.kpi-card')].find(el => el.textContent.includes(${JSON.stringify(label)})).click()`)
      await delay(250)
      assert.ok(await evaluate(`location.hash.startsWith('#/${destination}')`))
      assert.equal(await evaluate('document.querySelector("select[aria-label=\"Status filter\"]").value'), filter)
      if (label !== 'Parts in Stock') assert.equal(await evaluate('Number(document.querySelector(".count-label").textContent.split(" ")[0])'), count)
    })
  }
  await route('orders')
  await check('pagination, search reset, empty state and clear filters', async () => {
    await click('button[aria-label="Next page"]')
    assert.ok(await evaluate('document.querySelector(".pagination").textContent.includes("11–20")'))
    await fill('input[type="search"]', 'PO-2026-1067')
    assert.ok(await evaluate('document.querySelector(".pagination").textContent.includes("Page 1")'))
    assert.ok(await evaluate('[...document.querySelectorAll("tbody tr")].every(row => row.textContent.includes("PO-2026-1067"))'))
    await fill('input[type="search"]', 'no-such-order-xxxxxxxx')
    assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 0)
    await click('.filter-toolbar .text-button')
    assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 10)
  })
  await check('record details open, close with Escape and restore focus', async () => {
    await evaluate('document.querySelector(".record-link").focus()')
    await click('.record-link')
    assert.equal(await evaluate('document.querySelector("dialog").open'), true)
    await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 })
    await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 })
    await until('!document.querySelector("dialog")')
    assert.equal(await evaluate('document.activeElement.className'), 'record-link')
  })
  await check('date filtering operates on actual order dates', async () => {
    await fill('input[type="date"]', '2099-01-01')
    assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 0)
    await click('.filter-toolbar .text-button')
  })
  await route('inventory')
  await check('location filter and ten-row inventory pagination', async () => {
    const warehouse = await evaluate('document.querySelector("select[aria-label=\"Location filter\"]").options[1].value')
    await fill('select[aria-label="Location filter"]', warehouse)
    assert.ok(await evaluate(`[...document.querySelectorAll('tbody tr')].every(row => row.textContent.includes(${JSON.stringify(warehouse)}))`))
    assert.ok(await evaluate('document.querySelectorAll("tbody tr").length <= 10'))
  })
  await route('claims')
  await check('claims search and pagination', async () => {
    assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 10)
    await fill('input[type="search"]', 'CLM-4022')
    assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 1)
  })
  await route('knowledge')
  await check('real reference documents, search and reader', async () => {
    assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 10)
    await fill('input[type="search"]', 'Delivery Status Main List')
    await click('.record-link')
    assert.ok(await evaluate('document.querySelector("dialog").textContent.includes("delivery/api-delivery-status-main.md")'))
    await click('button[aria-label="Close dialog"]')
  })
  await check('navigation uses browser history', async () => {
    await route('orders'); await route('claims')
    await evaluate('history.back()')
    await until('document.querySelector("h1").textContent === "Orders"')
  })
  await check('floating assistant retains unsent draft across close and routes', async () => {
    await click('button[aria-label="Open AI Assistant"]')
    await fill('#chat-prompt', 'Is part P-10033 available?')
    await click('button[aria-label="Minimize assistant"]')
    await route('inventory')
    await click('button[aria-label="Open AI Assistant"]')
    assert.equal(await evaluate('document.querySelector("#chat-prompt").value'), 'Is part P-10033 available?')
  })
  if (process.env.UI_LIVE_CHAT === '1') {
    await check('chat error, retry, loading, duplicate protection and real API answer', async () => {
      await send('Network.setBlockedURLs', { urls: ['*api/assistant/chat*'] })
      await click('button[aria-label="Send message"]')
      await until('document.querySelector(".chat-message.failed")')
      await send('Network.setBlockedURLs', { urls: [] })
      await click('.chat-message.failed button')
      assert.equal(await evaluate('document.querySelector("button[aria-label=\"Send message\"]").disabled'), true)
      await until('document.querySelector(".message-sources")', 100000)
      assert.equal(await evaluate('document.querySelectorAll(".chat-message.user").length'), 1)
      assert.ok(await evaluate('document.querySelector(".chat-message.assistant").textContent.includes("P-10033")'))
      await click('button[aria-label="Minimize assistant"]'); await click('button[aria-label="Open AI Assistant"]')
      assert.equal(await evaluate('document.querySelectorAll(".chat-message.user").length'), 1)
    })
  }
  await click('button[aria-label="Minimize assistant"]')
  await check('mobile drawer, active route, chat fit and no page overflow', async () => {
    await send('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true })
    await route('dashboard')
    await click('button[aria-label="Open navigation"]')
    await click('.nav-drawer a[href="#/deliveries"]')
    assert.equal(await evaluate('document.querySelector("h1").textContent'), 'Deliveries')
    assert.equal(await evaluate('document.querySelector("dialog")'), null)
    assert.equal(await evaluate('document.documentElement.scrollWidth <= innerWidth'), true)
    await click('button[aria-label="Open AI Assistant"]')
    assert.ok(await evaluate('(() => { const r = document.querySelector(".chat-panel").getBoundingClientRect(); return r.left >= 0 && r.right <= innerWidth && r.top >= 0 && r.bottom <= innerHeight })()'))
    const mobile = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: false })
    writeFileSync(path.join(artifacts, 'assistant-mobile.png'), Buffer.from(mobile.data, 'base64'))
  })
  assert.deepEqual(exceptions, [], 'Browser runtime exceptions')
  console.log(`All UI smoke checks passed. Screenshots: ${artifacts}`)
} finally {
  if (socket?.readyState === WebSocket.OPEN) { try { await send('Browser.close') } catch {} socket.close() }
  browser.kill()
}
