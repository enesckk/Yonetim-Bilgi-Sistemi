import { useEffect, useState } from 'react'

/** Detect updates in long-lived tabs. Reload only after the user chooses to preserve unsaved work. */
export function AppUpdateNotice() {
  const [available, setAvailable] = useState(false)

  useEffect(() => {
    if (!('serviceWorker' in navigator)) return
    const workers = navigator.serviceWorker
    let controlled = workers.controller != null
    let disposed = false
    let checking = false
    let lastCheck = 0
    const controllerChanged = () => {
      if (controlled) setAvailable(true)
      controlled = workers.controller != null
    }
    const check = async () => {
      if (disposed || checking || document.visibilityState === 'hidden' || Date.now() - lastCheck < 60_000) return
      checking = true
      try {
        const registration = await workers.getRegistration()
        if (registration && !disposed) {
          lastCheck = Date.now()
          await registration.update()
        }
      } catch {
        // Offline checks are retried when focus/connectivity returns.
      } finally {
        checking = false
      }
    }
    workers.addEventListener('controllerchange', controllerChanged)
    window.addEventListener('focus', check)
    window.addEventListener('online', check)
    document.addEventListener('visibilitychange', check)
    const interval = window.setInterval(check, 5 * 60_000)
    void workers.ready.then(() => { if (!disposed) void check() }).catch(() => {})
    return () => {
      disposed = true
      workers.removeEventListener('controllerchange', controllerChanged)
      window.removeEventListener('focus', check)
      window.removeEventListener('online', check)
      document.removeEventListener('visibilitychange', check)
      window.clearInterval(interval)
    }
  }, [])

  if (!available) return null
  return (
    <aside className="app-update-notice" role="status" aria-live="polite">
      <span>Yeni sürüm hazır. Değişiklikleri görmek için sayfayı yenileyin.</span>
      <button type="button" className="btn-primary" onClick={() => window.location.reload()}>Yenile</button>
    </aside>
  )
}
