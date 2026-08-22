import { useEffect, useMemo, useRef, useState } from 'react'
import { AlertCircleIcon, CheckIcon, FileIcon, FolderIcon, FolderPlusIcon, HistoryIcon, PencilIcon, PlusIcon, SaveIcon, Trash2Icon, UploadIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { LazyMonacoSkillEditor } from '@/components/lazy-monaco-skill-editor'
import { ApiError, getSkillById, getSkillHistory, publishSkillVersion, readSkillFile, type SkillDetail, type SkillFileInfo, type SkillHistory, type SkillPathRename } from '@/lib/api'
import { SkillCreateForm } from './skill-create-form'
import { SkillEditorShell } from './skill-editor-shell'
import { SkillFilePreview } from './skill-file-preview'

type DraftContent = string | Blob

const isTextFile = (file: SkillFileInfo) => !file.binary

const foldersFromFiles = (files: SkillFileInfo[]) => [...new Set(files.flatMap((file) => {
  const parts = file.path.split('/')
  return parts.slice(0, -1).map((_, index) => parts.slice(0, index + 1).join('/'))
}))].sort()

async function toBase64(value: DraftContent) {
  const bytes = new Uint8Array(await new Blob([value]).arrayBuffer())
  let binary = ''
  for (let index = 0; index < bytes.length; index += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(index, index + 0x8000))
  }
  return window.btoa(binary)
}

export function SkillEditorPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { id } = useParams<{ id?: string }>()
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [history, setHistory] = useState<SkillHistory | null>(null)
  const [loading, setLoading] = useState(Boolean(id))
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  useEffect(() => {
    if (!id) {
      setDetail(null)
      setHistory(null)
      setLoading(false)
      return
    }

    let active = true
    setLoading(true)
    setError(null)
    void Promise.all([getSkillById(id), getSkillHistory(id)])
      .then(([loaded, loadedHistory]) => {
        if (!active) return
        setDetail(loaded)
        setHistory(loadedHistory)
      })
      .catch((cause) => {
        if (active) setError(cause instanceof Error ? cause.message : t('skills.editorLoadFailed'))
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => { active = false }
  }, [id, t])

  if (loading) {
    return <div className="mx-auto max-w-5xl py-12 text-sm text-muted-foreground">{t('skills.loadingEditor')}</div>
  }

  if (error && !detail) {
    return <SkillEditorShell title={t('skills.editorPreviewTitle')}><Alert variant="destructive"><AlertCircleIcon className="size-4" /><AlertTitle>{t('skills.editorLoadFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert></SkillEditorShell>
  }

  if (!detail) {
    return <SkillCreateForm onCreated={(created) => navigate(`/skills/editor/${created.id}`, { replace: true })} />
  }

  return (
    <SkillVersionEditor
      detail={detail}
      history={history}
      error={error}
      notice={notice}
      onError={setError}
      onNotice={setNotice}
      onPublished={(created) => {
        setDetail(created)
        setNotice(t('skills.versionPublished', { version: created.version }))
        void getSkillHistory(created.id).then(setHistory)
        navigate(`/skills/editor/${created.id}`, { replace: true })
      }}
      onSelectVersion={(versionId) => {
        setNotice(null)
        navigate(`/skills/editor/${versionId}`)
      }}
    />
  )
}

interface SkillVersionEditorProps {
  detail: SkillDetail
  history: SkillHistory | null
  error: string | null
  notice: string | null
  onError: (value: string | null) => void
  onNotice: (value: string | null) => void
  onPublished: (detail: SkillDetail) => void
  onSelectVersion: (id: string) => void
}

function SkillVersionEditor({ detail, history, error, notice, onError, onNotice, onPublished, onSelectVersion }: SkillVersionEditorProps) {
  const { t } = useTranslation()
  const [name, setName] = useState(detail.name)
  const [description, setDescription] = useState(detail.description)
  const [instructions, setInstructions] = useState('')
  const [files, setFiles] = useState(detail.manifest)
  const [folders, setFolders] = useState(detail.folders?.length ? detail.folders : foldersFromFiles(detail.manifest))
  const [activePath, setActivePath] = useState('SKILL.md')
  const [activeContent, setActiveContent] = useState('')
  const [previewing, setPreviewing] = useState(true)
  const [overrides, setOverrides] = useState<Record<string, DraftContent>>({})
  const [renames, setRenames] = useState<SkillPathRename[]>([])
  const [deletedPaths, setDeletedPaths] = useState<string[]>([])
  const [savingFile, setSavingFile] = useState(false)
  const [publishing, setPublishing] = useState(false)
  const fileInput = useRef<HTMLInputElement>(null)
  const originalFiles = useRef(new Set(detail.manifest.map((file) => file.path)))
  const originalFolders = useRef(new Set(detail.folders ?? foldersFromFiles(detail.manifest)))

  useEffect(() => {
    let active = true
    setName(detail.name)
    setDescription(detail.description)
    setFiles(detail.manifest)
    setFolders(detail.folders?.length ? detail.folders : foldersFromFiles(detail.manifest))
    originalFiles.current = new Set(detail.manifest.map((file) => file.path))
    originalFolders.current = new Set(detail.folders ?? foldersFromFiles(detail.manifest))
    setOverrides({})
    setRenames([])
    setDeletedPaths([])
    setActivePath('SKILL.md')
    setPreviewing(true)
    void readSkillFile(detail.id, 'SKILL.md').then(async (blob) => {
      if (active) {
        const text = await blob.text()
        setInstructions(text)
        setActiveContent(text)
      }
    }).catch(() => { if (active) setInstructions('') })
    return () => { active = false }
  }, [detail])

  const activeInfo = files.find((file) => file.path === activePath)
  const sortedFiles = useMemo(() => [...files].sort((a, b) => a.path.localeCompare(b.path)), [files])

  const selectFile = async (path: string) => {
    setActivePath(path)
    setPreviewing(true)
    setActiveContent('')
    const staged = overrides[path]
    if (typeof staged === 'string') {
      setActiveContent(staged)
      if (path === 'SKILL.md') setInstructions(staged)
      return
    }
    const info = files.find((file) => file.path === path)
    if (!info || !isTextFile(info)) return
    try {
      const text = await (await readSkillFile(detail.id, path)).text()
      setActiveContent(text)
      if (path === 'SKILL.md') setInstructions(text)
    } catch (cause) {
      onError(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'))
    }
  }

  const saveActiveFile = () => {
    if (!activeInfo || activeInfo.binary) return
    setOverrides((current) => ({ ...current, [activePath]: activeContent }))
    if (activePath === 'SKILL.md') setInstructions(activeContent)
    onNotice(t('skills.draftSaved'))
  }

  const addFile = () => {
    const path = window.prompt(t('skills.filePath'))?.trim()
    if (!path || path.includes('..') || path.startsWith('/') || path.endsWith('/')) return
    setFiles((current) => current.some((file) => file.path === path) ? current : [...current, { path, size: 0, binary: false }])
    setOverrides((current) => ({ ...current, [path]: '' }))
    setActivePath(path)
    setActiveContent('')
    setPreviewing(false)
  }

  const addFolder = () => {
    const path = window.prompt(t('skills.folderName'))?.trim()
    if (!path || path.includes('..') || path.startsWith('/') || path.endsWith('/')) return
    setFolders((current) => current.includes(path) ? current : [...current, path].sort())
    onNotice(t('skills.draftSaved'))
  }

  const renamePath = (from: string, folder: boolean) => {
    const to = window.prompt(t('skills.renameFile'), from)?.trim()
    if (!to || to === from || to.includes('..') || to.startsWith('/')) return
    if (folder) {
      setFolders((current) => current.map((path) => path === from || path.startsWith(`${from}/`) ? to + path.slice(from.length) : path))
      setFiles((current) => current.map((file) => file.path.startsWith(`${from}/`) ? { ...file, path: to + file.path.slice(from.length) } : file))
      setOverrides((current) => Object.fromEntries(Object.entries(current).map(([path, value]) => [path.startsWith(`${from}/`) ? to + path.slice(from.length) : path, value])))
    } else {
      setFiles((current) => current.map((file) => file.path === from ? { ...file, path: to } : file))
      setOverrides((current) => {
        const next = { ...current }
        if (from in next) { next[to] = next[from]; delete next[from] }
        return next
      })
    }
    setRenames((current) => {
      const prior = current.find((rename) => rename.to === from)
      const shouldTrack = originalFiles.current.has(from) || originalFolders.current.has(from) || prior !== undefined
      if (!shouldTrack) return current
      const withoutPrior = prior ? current.filter((rename) => rename !== prior) : current
      return [...withoutPrior, { from: prior?.from ?? from, to }]
    })
    if (activePath === from || (folder && activePath.startsWith(`${from}/`))) setActivePath(to + activePath.slice(from.length))
    onNotice(t('skills.draftSaved'))
  }

  const removePath = (path: string, folder = false) => {
    if (!window.confirm(t('skills.deletePathConfirm', { path }))) return
    if (folder) {
      setFolders((current) => current.filter((value) => value !== path && !value.startsWith(`${path}/`)))
      setFiles((current) => current.filter((file) => !file.path.startsWith(`${path}/`)))
    } else {
      setFiles((current) => current.filter((file) => file.path !== path))
    }
    setOverrides((current) => Object.fromEntries(Object.entries(current).filter(([value]) => value !== path && !value.startsWith(`${path}/`))))
    if (originalFiles.current.has(path)
      || originalFolders.current.has(path)
      || renames.some((rename) => rename.to === path || rename.to.startsWith(`${path}/`))) {
      setDeletedPaths((current) => [...new Set([...current, path])])
    }
    if (activePath === path) void selectFile('SKILL.md')
  }

  const uploadFiles = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const selected = [...(event.target.files ?? [])]
    for (const file of selected) {
      const path = file.name
      setFiles((current) => current.some((item) => item.path === path) ? current : [...current, { path, size: file.size, binary: !file.type.startsWith('text/') }])
      setOverrides((current) => ({ ...current, [path]: file }))
    }
    event.target.value = ''
    onNotice(t('skills.draftSaved'))
  }

  const publish = async () => {
    if (!detail.isLatest) return
    setPublishing(true)
    onError(null)
    onNotice(null)
    try {
      const files = await Promise.all(Object.entries(overrides).map(async ([path, content]) => ({ path, contentBase64: await toBase64(content) })))
      onPublished(await publishSkillVersion(detail.id, { name, description, instructions, files, folders, renames, deletedPaths }))
    } catch (cause) {
      if (cause instanceof ApiError && cause.status === 409 && typeof cause.details.latestId === 'string') {
        onError(`${cause.message} ${t('skills.latestVersionHint', { version: String(cause.details.latestVersion ?? '') })}`)
      } else onError(cause instanceof Error ? cause.message : t('skills.editorSaveFailed'))
    } finally {
      setPublishing(false)
    }
  }

  return <SkillEditorShell title={detail.name}>
    <div className="mb-4 flex flex-wrap items-center gap-2"><Badge variant="accent">{detail.domainName}</Badge><Badge variant="outline" className="font-mono">{detail.slug}</Badge><Badge variant="outline">{t('skills.version', { version: detail.version })}</Badge>{detail.isLatest ? <Badge variant="outline" className="text-ok"><CheckIcon className="mr-1 size-3" />{t('skills.latest')}</Badge> : <Badge variant="destructive">{t('skills.historicalReadOnly')}</Badge>}</div>
    {error && <Alert variant="destructive" className="mb-4"><AlertCircleIcon className="size-4" /><AlertDescription>{error}</AlertDescription></Alert>}
    {notice && <Alert className="mb-4"><AlertDescription>{notice}</AlertDescription></Alert>}
    {!detail.isLatest && <Alert className="mb-4"><HistoryIcon className="size-4" /><AlertDescription>{t('skills.historicalDescription')}</AlertDescription></Alert>}
    <div className="grid gap-5 xl:grid-cols-[minmax(0,1fr)_250px]">
      <div className="grid gap-5">
        <Card><CardHeader><CardTitle className="text-base">{t('skills.metadata')}</CardTitle></CardHeader><CardContent className="grid gap-4 sm:grid-cols-2"><label className="grid gap-1.5"><Label>{t('skills.editor.name')}</Label><Input value={name} onChange={(event) => setName(event.target.value)} disabled={!detail.isLatest} /></label><label className="grid gap-1.5"><Label>{t('skills.editor.description')}</Label><Input value={description} onChange={(event) => setDescription(event.target.value)} disabled={!detail.isLatest} /></label></CardContent></Card>
        <Card><CardHeader className="flex flex-row items-center gap-2"><CardTitle className="text-base">{t('skills.packageTree')}</CardTitle><div className="ml-auto flex gap-1"><Button size="sm" variant="outline" disabled={!detail.isLatest} onClick={addFile}><PlusIcon className="mr-1 size-3.5" />{t('skills.newFile')}</Button><Button size="sm" variant="outline" disabled={!detail.isLatest} onClick={addFolder}><FolderPlusIcon className="mr-1 size-3.5" />{t('skills.newFolder')}</Button><Button size="sm" variant="outline" disabled={!detail.isLatest} onClick={() => fileInput.current?.click()}><UploadIcon className="mr-1 size-3.5" />{t('skills.uploadFiles')}</Button><input ref={fileInput} type="file" multiple className="hidden" onChange={(event) => void uploadFiles(event)} /></div></CardHeader><CardContent className="grid gap-3 lg:grid-cols-[220px_minmax(0,1fr)]"><div className="space-y-1 rounded-xl border border-border/70 bg-muted/20 p-2">{sortedFiles.map((file) => <div key={file.path} className={`group flex items-center gap-2 rounded-md px-2 py-1.5 text-xs ${activePath === file.path ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted'}`}><button type="button" className="flex min-w-0 flex-1 items-center gap-2 text-left" onClick={() => void selectFile(file.path)}><FileIcon className="size-3.5 shrink-0" /><span className="truncate font-mono">{file.path}</span></button>{detail.isLatest && <button type="button" title={t('skills.renameFile')} onClick={() => renamePath(file.path, false)}><PencilIcon className="size-3" /></button>} {detail.isLatest && <button type="button" title={t('common.delete')} onClick={() => removePath(file.path)}><Trash2Icon className="size-3 text-destructive" /></button>}</div>)}{folders.map((folder) => <div key={folder} className="flex items-center gap-2 rounded-md px-2 py-1.5 text-xs text-muted-foreground"><FolderIcon className="size-3.5 shrink-0" /><span className="min-w-0 flex-1 truncate font-mono">{folder}</span>{detail.isLatest && <button type="button" title={t('skills.renameFile')} onClick={() => renamePath(folder, true)}><PencilIcon className="size-3" /></button>} {detail.isLatest && <button type="button" title={t('common.delete')} onClick={() => removePath(folder, true)}><Trash2Icon className="size-3 text-destructive" /></button>}</div>)}</div><div className="min-h-[300px]">{activeInfo?.binary ? <p className="rounded-lg border border-border/70 p-4 text-xs text-muted-foreground">{t('skills.binaryFileNote')}</p> : <><div className="mb-3 flex flex-wrap items-center justify-between gap-2"><span className="font-mono text-xs text-muted-foreground">{activePath}</span><div className="flex items-center gap-1 rounded-lg border border-border/70 bg-muted/20 p-1"><Button size="sm" variant={previewing ? 'secondary' : 'ghost'} aria-pressed={previewing} onClick={() => setPreviewing(true)}>{t('skills.preview')}</Button><Button size="sm" variant={!previewing ? 'secondary' : 'ghost'} aria-pressed={!previewing} disabled={!detail.isLatest} onClick={() => setPreviewing(false)}>{t('skills.edit')}</Button>{!previewing && <Button size="sm" variant="outline" disabled={!detail.isLatest} onClick={() => { saveActiveFile(); setPreviewing(true); setSavingFile(false) }}>{savingFile ? t('skills.saving') : <><SaveIcon className="mr-1 size-3.5" />{t('skills.saveDraft')}</>}</Button>}</div></div>{previewing ? <SkillFilePreview path={activePath} content={activeContent} /> : <LazyMonacoSkillEditor key={activePath} path={activePath} value={activeContent} disabled={!detail.isLatest} onChange={setActiveContent} />}</>}</div></CardContent></Card>
      </div>
      <div className="grid h-fit gap-5"><Card><CardHeader><CardTitle className="flex items-center gap-2 text-base"><HistoryIcon className="size-4" />{t('skills.history')}</CardTitle></CardHeader><CardContent className="space-y-1">{history?.versions.map((version) => <button key={version.id} type="button" className={`flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-xs hover:bg-muted ${version.id === detail.id ? 'bg-primary/10 text-primary' : ''}`} onClick={() => onSelectVersion(version.id)}><span><span className="font-medium">{t('skills.version', { version: version.version })}</span><span className="mt-0.5 block text-[10px] text-muted-foreground">{version.name}</span></span>{version.isLatest && <Badge variant="outline" className="text-[9px]">{t('skills.latest')}</Badge>}</button>)}</CardContent></Card><Card><CardHeader><CardTitle className="text-base">{t('skills.publishTitle')}</CardTitle></CardHeader><CardContent className="space-y-3 text-xs text-muted-foreground"><p>{t('skills.publishDescription')}</p><Button className="w-full" disabled={!detail.isLatest || publishing} onClick={() => void publish()}>{publishing ? t('skills.publishing') : t('skills.publishVersion')}</Button>{!detail.isLatest && <p className="text-destructive">{t('skills.historicalReadOnly')}</p>}</CardContent></Card></div>
    </div>
  </SkillEditorShell>
}
