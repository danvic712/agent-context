import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { createSkill, type SkillDetail } from '@/lib/api'
import { SkillEditorShell } from './skill-editor-shell'

export function SkillCreateForm({ onCreated }: { onCreated: (created: SkillDetail) => void }) {
  const { t } = useTranslation()
  const [values, setValues] = useState({ domain: '', slug: '', name: '', description: '', instructions: '' })
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const update = (key: keyof typeof values, value: string) => setValues((current) => ({ ...current, [key]: value }))
  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setSaving(true)
    setError(null)
    try {
      onCreated(await createSkill(values))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.editorSaveFailed'))
    } finally {
      setSaving(false)
    }
  }

  return <SkillEditorShell title={t('skills.editorCreateTitle')}>
    <form onSubmit={submit} className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_260px]">
      <Card><CardHeader><CardTitle className="text-base">{t('skills.createMetadata')}</CardTitle></CardHeader><CardContent className="grid gap-4 sm:grid-cols-2">
        {(['domain', 'slug', 'name', 'description'] as const).map((key) => <label key={key} className="grid gap-1.5"><span className="text-xs font-medium">{t(`skills.editor.${key}`)}</span><Input value={values[key]} onChange={(event) => update(key, event.target.value)} required={key !== 'description'} /></label>)}
        <label className="grid gap-1.5 sm:col-span-2"><span className="text-xs font-medium">{t('skills.editor.instructions')}</span><Textarea value={values.instructions} onChange={(event) => update('instructions', event.target.value)} rows={16} className="font-mono text-xs" /></label>
        {error && <Alert variant="destructive" className="sm:col-span-2"><AlertDescription>{error}</AlertDescription></Alert>}
        <div className="flex justify-end gap-2 sm:col-span-2"><Link to="/skills" className="inline-flex h-8 items-center rounded-lg px-3 text-xs text-muted-foreground hover:bg-muted">{t('common.cancel')}</Link><Button type="submit" disabled={saving}>{saving ? t('skills.saving') : t('skills.createSkill')}</Button></div>
      </CardContent></Card>
      <Card className="h-fit"><CardHeader><CardTitle className="text-base">{t('skills.versioningTitle')}</CardTitle></CardHeader><CardContent className="text-xs leading-5 text-muted-foreground">{t('skills.versioningDescription')}</CardContent></Card>
    </form>
  </SkillEditorShell>
}
