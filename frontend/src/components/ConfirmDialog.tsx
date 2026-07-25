import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import { createPortal } from 'react-dom'

export type ConfirmOptions = {
  title?: string
  message: string
  confirmLabel?: string
  cancelLabel?: string
  /** Varsayılan: danger (silme). reset/uyarı için 'primary' */
  tone?: 'danger' | 'primary'
}

export type AlertOptions = {
  title?: string
  message: string
  okLabel?: string
  /** Varsayılan: warning */
  tone?: 'warning' | 'danger' | 'info'
}

type ConfirmFn = (options: ConfirmOptions | string) => Promise<boolean>
type AlertFn = (options: AlertOptions | string) => Promise<void>

const ConfirmContext = createContext<ConfirmFn | null>(null)
const AlertContext = createContext<AlertFn | null>(null)

type ConfirmPending = ConfirmOptions & { resolve: (value: boolean) => void }
type AlertPending = AlertOptions & { resolve: () => void }

function normalizeConfirm(options: ConfirmOptions | string): ConfirmOptions {
  if (typeof options === 'string') return { message: options }
  return options
}

function normalizeAlert(options: AlertOptions | string): AlertOptions {
  if (typeof options === 'string') return { message: options }
  return options
}

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [confirmPending, setConfirmPending] = useState<ConfirmPending | null>(null)
  const confirmRef = useRef<ConfirmPending | null>(null)
  const [alertPending, setAlertPending] = useState<AlertPending | null>(null)
  const alertRef = useRef<AlertPending | null>(null)

  const closeConfirm = useCallback((value: boolean) => {
    const current = confirmRef.current
    confirmRef.current = null
    setConfirmPending(null)
    current?.resolve(value)
  }, [])

  const closeAlert = useCallback(() => {
    const current = alertRef.current
    alertRef.current = null
    setAlertPending(null)
    current?.resolve()
  }, [])

  const confirm = useCallback<ConfirmFn>((options) => {
    if (confirmRef.current) confirmRef.current.resolve(false)
    return new Promise<boolean>((resolve) => {
      const next: ConfirmPending = { ...normalizeConfirm(options), resolve }
      confirmRef.current = next
      setConfirmPending(next)
    })
  }, [])

  const alert = useCallback<AlertFn>((options) => {
    if (alertRef.current) alertRef.current.resolve()
    return new Promise<void>((resolve) => {
      const next: AlertPending = { ...normalizeAlert(options), resolve }
      alertRef.current = next
      setAlertPending(next)
    })
  }, [])

  const open = Boolean(confirmPending || alertPending)

  useEffect(() => {
    if (!open) return
    const scrollY = window.scrollY
    const { overflow, position, top, width } = document.body.style
    document.body.style.overflow = 'hidden'
    document.body.style.position = 'fixed'
    document.body.style.top = `-${scrollY}px`
    document.body.style.width = '100%'
    function onKey(e: KeyboardEvent) {
      if (e.key !== 'Escape') return
      if (alertPending) closeAlert()
      else closeConfirm(false)
    }
    document.addEventListener('keydown', onKey)
    return () => {
      document.body.style.overflow = overflow
      document.body.style.position = position
      document.body.style.top = top
      document.body.style.width = width
      window.scrollTo(0, scrollY)
      document.removeEventListener('keydown', onKey)
    }
  }, [open, alertPending, closeAlert, closeConfirm])

  const confirmValue = useMemo(() => confirm, [confirm])
  const alertValue = useMemo(() => alert, [alert])

  const alertTone = alertPending?.tone ?? 'warning'
  const alertEyebrow =
    alertTone === 'danger' ? 'Hata' : alertTone === 'info' ? 'Bilgi' : 'Uyarı'

  return (
    <ConfirmContext.Provider value={confirmValue}>
      <AlertContext.Provider value={alertValue}>
        {children}

        {confirmPending
          ? createPortal(
              <div
                className="confirm-backdrop"
                role="presentation"
                onMouseDown={(e) => {
                  if (e.target === e.currentTarget) closeConfirm(false)
                }}
              >
                <div
                  className="confirm-dialog panel"
                  role="alertdialog"
                  aria-modal="true"
                  aria-labelledby="confirm-dialog-title"
                  aria-describedby="confirm-dialog-message"
                >
                  <p className="org-modal-eyebrow">Onay</p>
                  <h2 id="confirm-dialog-title">{confirmPending.title ?? 'Emin misiniz?'}</h2>
                  <p id="confirm-dialog-message" className="confirm-message">
                    {confirmPending.message}
                  </p>
                  <div className="confirm-actions">
                    <button
                      type="button"
                      className={confirmPending.tone === 'primary' ? 'btn-primary' : 'btn-danger'}
                      autoFocus
                      onClick={() => closeConfirm(true)}
                    >
                      {confirmPending.confirmLabel ?? 'Sil'}
                    </button>
                    <button type="button" className="btn-secondary" onClick={() => closeConfirm(false)}>
                      {confirmPending.cancelLabel ?? 'Vazgeç'}
                    </button>
                  </div>
                </div>
              </div>,
              document.body,
            )
          : null}

        {alertPending
          ? createPortal(
              <div
                className="confirm-backdrop"
                role="presentation"
                onMouseDown={(e) => {
                  if (e.target === e.currentTarget) closeAlert()
                }}
              >
                <div
                  className={`confirm-dialog panel alert-dialog tone-${alertTone}`}
                  role="alertdialog"
                  aria-modal="true"
                  aria-labelledby="alert-dialog-title"
                  aria-describedby="alert-dialog-message"
                >
                  <p className="org-modal-eyebrow">{alertEyebrow}</p>
                  <h2 id="alert-dialog-title">{alertPending.title ?? 'Bilgilendirme'}</h2>
                  <p id="alert-dialog-message" className="confirm-message">
                    {alertPending.message}
                  </p>
                  <div className="confirm-actions">
                    <button
                      type="button"
                      className="btn-primary"
                      autoFocus
                      onClick={() => closeAlert()}
                    >
                      {alertPending.okLabel ?? 'Tamam'}
                    </button>
                  </div>
                </div>
              </div>,
              document.body,
            )
          : null}
      </AlertContext.Provider>
    </ConfirmContext.Provider>
  )
}

export function useConfirm(): ConfirmFn {
  const ctx = useContext(ConfirmContext)
  if (!ctx) throw new Error('useConfirm yalnızca ConfirmProvider içinde kullanılabilir.')
  return ctx
}

export function useAlert(): AlertFn {
  const ctx = useContext(AlertContext)
  if (!ctx) throw new Error('useAlert yalnızca ConfirmProvider içinde kullanılabilir.')
  return ctx
}
