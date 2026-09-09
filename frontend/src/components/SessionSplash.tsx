export function SessionSplash({ message = 'Oturum kontrol ediliyor…' }: { message?: string }) {
  return (
    <div className="session-splash" role="status" aria-live="polite">
      <img src="/logo.svg?v=4" alt="" width={48} height={48} />
      <strong>Yönetim Bilgi Sistemi</strong>
      <p>{message}</p>
    </div>
  )
}
