export function ActiveCustodyWarning({ message, acknowledged, onChange }: {
  message: string; acknowledged: boolean; onChange: (value: boolean) => void
}) {
  if (!message) return null
  return <div className="span-2" role="alert">
    <p className="form-error">{message}</p>
    <label style={{ display: 'flex', alignItems: 'flex-start', gap: 8 }}>
      <input type="checkbox" checked={acknowledged} onChange={e => onChange(e.target.checked)} style={{ width: 'auto', marginTop: 4 }} />
      <span>Zimmetlerin açık kalacağını biliyorum; personel işlemine devam et.</span>
    </label>
  </div>
}
