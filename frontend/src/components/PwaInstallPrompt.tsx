import { useEffect, useState } from 'react'

type InstallChoice = { outcome: 'accepted' | 'dismissed' }

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>
  userChoice: Promise<InstallChoice>
}

const DISMISSED_KEY = 'ybs.pwa.install.dismissed'

function runsStandalone() {
  const navigatorWithStandalone = navigator as Navigator & { standalone?: boolean }
  return window.matchMedia('(display-mode: standalone)').matches || navigatorWithStandalone.standalone === true
}

function isIosDevice() {
  return /iPad|iPhone|iPod/.test(navigator.userAgent) ||
    (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1)
}

export function PwaInstallPrompt() {
  const [installEvent, setInstallEvent] = useState<BeforeInstallPromptEvent | null>(null)
  const [visible, setVisible] = useState(false)
  const [showInstructions, setShowInstructions] = useState(false)
  const [isIos] = useState(isIosDevice)

  useEffect(() => {
    if (runsStandalone()) return

    let dismissed = false
    try {
      dismissed = sessionStorage.getItem(DISMISSED_KEY) === '1'
    } catch {
      // Gizli sekmede depolama kapalı olabilir.
    }

    const mobile = window.matchMedia('(max-width: 860px), (pointer: coarse)').matches
    if (mobile && !dismissed) setVisible(true)

    const onBeforeInstall = (event: Event) => {
      event.preventDefault()
      setInstallEvent(event as BeforeInstallPromptEvent)
      if (!dismissed) setVisible(true)
    }
    const onInstalled = () => {
      setInstallEvent(null)
      setVisible(false)
      setShowInstructions(false)
    }

    window.addEventListener('beforeinstallprompt', onBeforeInstall)
    window.addEventListener('appinstalled', onInstalled)
    return () => {
      window.removeEventListener('beforeinstallprompt', onBeforeInstall)
      window.removeEventListener('appinstalled', onInstalled)
    }
  }, [])

  function dismiss() {
    try {
      sessionStorage.setItem(DISMISSED_KEY, '1')
    } catch {
      // Görünürlüğü kapatmak için depolama zorunlu değil.
    }
    setVisible(false)
  }

  async function install() {
    if (installEvent) {
      await installEvent.prompt()
      const choice = await installEvent.userChoice
      if (choice.outcome === 'accepted') {
        setVisible(false)
        setInstallEvent(null)
      }
      return
    }
    setShowInstructions(true)
  }

  if (!visible) return null

  return (
    <section className="pwa-install-card" aria-label="Uygulamayı telefona ekle">
      <img src="/pwa-192x192.png" width={48} height={48} alt="YBS logosu" />
      <div className="pwa-install-copy">
        <strong>YBS’yi telefona ekleyin</strong>
        {showInstructions ? (
          <p>
            {isIos
              ? 'Safari’de Paylaş simgesine dokunun, ardından “Ana Ekrana Ekle”yi seçin.'
              : 'Tarayıcı menüsünü açıp “Uygulamayı yükle” veya “Ana ekrana ekle”yi seçin.'}
          </p>
        ) : (
          <p>Logosuyla ana ekranınızdan tek dokunuşla açın.</p>
        )}
      </div>
      <div className="pwa-install-actions">
        <button
          type="button"
          className="pwa-install-primary"
          onClick={showInstructions ? dismiss : () => void install()}
        >
          {showInstructions ? 'Anladım' : 'Uygulamayı ekle'}
        </button>
        <button type="button" className="pwa-install-close" onClick={dismiss} aria-label="Kurulum önerisini kapat">
          ×
        </button>
      </div>
    </section>
  )
}
