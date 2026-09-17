import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'
import { Icon } from './Icon'

export function Dialog({ title, onClose, children, className = '' }: { title: string; onClose: () => void; children: ReactNode; className?: string }) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    const dialog = ref.current
    dialog?.showModal()
    return () => { dialog?.close(); previous?.focus() }
  }, [])
  return <dialog ref={ref} className={`dialog ${className}`} aria-labelledby={titleId} onCancel={onClose} onClick={event => { if (event.target === event.currentTarget) onClose() }}>
    <div className="dialog-head"><h2 id={titleId}>{title}</h2><button className="icon-button" onClick={onClose} aria-label="Close dialog"><Icon name="close" /></button></div>
    <div className="dialog-body">{children}</div>
  </dialog>
}

export function RecordDetails({ title, fields, onClose, onAsk }: { title: string; fields: [string, string][]; onClose: () => void; onAsk: () => void }) {
  return <Dialog title={title} onClose={onClose}>
    <p className="muted">Current record from the operations API</p>
    <dl className="detail-list">{fields.map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value || 'Not recorded'}</dd></div>)}</dl>
    <button className="primary" onClick={onAsk}><Icon name="assistant" /> Ask about this record</button>
  </Dialog>
}
