import { FileIcon, UploadCloudIcon, XIcon } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { LazyMonacoSkillEditor } from '@/components/lazy-monaco-skill-editor'
import { uploadSkillFiles, createSkill, type SkillDetail } from '@/lib/api'
import { SkillEditorShell } from './skill-editor-shell'
import { SkillLanguageBadge } from './skill-language-badge'

export function SkillCreateForm({ onCreated }: { onCreated: (created: SkillDetail) => void }) {
  const { t } = useTranslation()
  const [values, setValues] = useState({ domain: '', slug: '', name: '', description: '', instructions: '' })
  const [saving, setSaving] = useState(false)
  const [files, setFiles] = useState<File[]>([])
  const [error, setError] = useState<string | null>(null)
  const fileInput = useRef<HTMLInputElement>(null)
  const update = (key: keyof typeof values, value: string) => setValues((current) => ({ ...current, [key]: value }))
  const chooseFiles = (selected: FileList | null) => {
    const picked = [...(selected ?? [])]
    const includesMainFile = picked.some((file) => file.name.toLowerCase() === 'skill.md')
    const additional = picked.filter((file) => file.name.toLowerCase() !== 'skill.md')
    if (includesMainFile) setError(t('skills.mainFileAlreadyDefined'))
    setFiles((current) => {
      const existing = new Set(current.map((file) => `${file.name}:${file.size}:${file.lastModified}`))
      return [...current, ...additional.filter((file) => !existing.has(`${file.name}:${file.size}:${file.lastModified}`))]
    })
    if (fileInput.current) fileInput.current.value = ''
  }
  const removeFile = (fileToRemove: File) => setFiles((current) => current.filter((file) => file !== fileToRemove))
  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const created = await createSkill(values)
      onCreated(files.length ? await uploadSkillFiles(created.id, files) : created)
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
        <div className="grid gap-1.5 sm:col-span-2"><div className="flex items-center justify-between gap-2"><span className="text-xs font-medium">{t('skills.editor.instructions')}</span><SkillLanguageBadge path="SKILL.md" /></div><LazyMonacoSkillEditor path="SKILL.md" value={values.instructions} onChange={(value) => update('instructions', value)} /></div>
        <div className="grid gap-2 sm:col-span-2"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-xs font-medium">{t('skills.additionalFiles')}</p><p className="mt-1 text-xs text-muted-foreground">{t('skills.additionalFilesHint')}</p></div><Button type="button" variant="outline" size="sm" onClick={() => fileInput.current?.click()}><UploadCloudIcon className="mr-1.5 size-3.5" />{t('skills.chooseFiles')}</Button></div><input ref={fileInput} type="file" multiple className="hidden" onChange={(event) => chooseFiles(event.target.files)} />{files.length > 0 && <div className="grid gap-2 rounded-xl border border-border/70 bg-muted/20 p-2">{files.map((file) => <div key={`${file.name}:${file.size}:${file.lastModified}`} className="flex items-center gap-2 rounded-lg bg-card px-2.5 py-2 text-xs"><FileIcon className="size-3.5 shrink-0 text-muted-foreground" /><span className="min-w-0 flex-1 truncate font-mono">{file.name}</span><span className="shrink-0 text-[10px] text-muted-foreground">{Math.max(1, Math.round(file.size / 1024))} KB</span><Button type="button" size="icon-xs" variant="ghost" aria-label={t('skills.removeFile', { name: file.name })} onClick={() => removeFile(file)}><XIcon /></Button></div>)}</div>}</div>
        {error && <Alert variant="destructive" className="sm:col-span-2"><AlertDescription>{error}</AlertDescription></Alert>}
        <div className="flex justify-end gap-2 sm:col-span-2"><Link to="/skills" className="inline-flex h-8 items-center rounded-lg px-3 text-xs text-muted-foreground hover:bg-muted">{t('common.cancel')}</Link><Button type="submit" disabled={saving}>{saving ? t('skills.saving') : t('skills.createSkill')}</Button></div>
      </CardContent></Card>
      <Card className="h-fit"><CardHeader><CardTitle className="text-base">{t('skills.versioningTitle')}</CardTitle></CardHeader><CardContent className="text-xs leading-5 text-muted-foreground">{t('skills.versioningDescription')}</CardContent></Card>
    </form>
  </SkillEditorShell>
}
