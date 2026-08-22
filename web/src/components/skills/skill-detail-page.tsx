import { useEffect, useMemo, useState } from 'react'
import { AlertCircleIcon, ArrowLeftIcon, DownloadIcon, FileIcon, FolderIcon, LoaderCircleIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { SkillFilePreview } from './skill-file-preview'
import { SkillLanguageBadge } from './skill-language-badge'
import { getSkillById, readSkillFile, type SkillDetail, type SkillFileInfo } from '@/lib/api'

const fileName = (path: string) => path.split('/').pop() ?? path

const sortFiles = (files: SkillFileInfo[]) => [...files].sort((a, b) =>
  fileName(a.path).localeCompare(fileName(b.path), undefined, { numeric: true, sensitivity: 'base' })
    || a.path.localeCompare(b.path, undefined, { numeric: true, sensitivity: 'base' }),
)

const foldersFromFiles = (files: SkillFileInfo[]) => [...new Set(files.flatMap((file) => {
  const parts = file.path.split('/')
  return parts.slice(0, -1).map((_, index) => parts.slice(0, index + 1).join('/'))
}))].sort((a, b) => a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' }))

export function SkillDetailPage() {
  const { t } = useTranslation()
  const { id } = useParams<{ id: string }>()
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [activePath, setActivePath] = useState('')
  const [fileContent, setFileContent] = useState('')
  const [loading, setLoading] = useState(true)
  const [loadingFile, setLoadingFile] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) {
      setError(t('skills.detailLoadFailed'))
      setLoading(false)
      return
    }

    let active = true
    setLoading(true)
    setError(null)
    void getSkillById(id)
      .then((loaded) => {
        if (!active) return
        setDetail(loaded)
        setActivePath(loaded.manifest.find((file) => file.path === 'SKILL.md')?.path ?? loaded.manifest[0]?.path ?? '')
      })
      .catch((cause) => {
        if (active) setError(cause instanceof Error ? cause.message : t('skills.detailLoadFailed'))
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => { active = false }
  }, [id, t])

  const folders = useMemo(
    () => detail?.folders?.length ? [...detail.folders].sort((a, b) => a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' })) : foldersFromFiles(detail?.manifest ?? []),
    [detail],
  )
  const files = useMemo(() => sortFiles(detail?.manifest ?? []), [detail])
  const activeInfo = files.find((file) => file.path === activePath)

  useEffect(() => {
    if (!detail || !activeInfo) return
    let active = true
    setLoadingFile(true)
    setError(null)
    void readSkillFile(detail.id, activeInfo.path)
      .then(async (blob) => {
        if (!active) return
        setFileContent(activeInfo.binary ? '' : await blob.text())
      })
      .catch((cause) => {
        if (active) setError(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'))
      })
      .finally(() => {
        if (active) setLoadingFile(false)
      })
    return () => { active = false }
  }, [activeInfo, detail, t])

  const downloadActive = async () => {
    if (!detail || !activeInfo) return
    const blob = await readSkillFile(detail.id, activeInfo.path)
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = fileName(activeInfo.path)
    anchor.click()
    URL.revokeObjectURL(url)
  }

  if (loading) {
    return <div className="mx-auto max-w-5xl py-12 text-sm text-muted-foreground">{t('skills.loadingDetail')}</div>
  }

  if (error && !detail) {
    return (
      <div className="mx-auto max-w-5xl">
        <Alert variant="destructive">
          <AlertCircleIcon className="size-4" />
          <AlertTitle>{t('skills.detailLoadFailed')}</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      </div>
    )
  }

  if (!detail) return null

  return (
    <div className="mx-auto max-w-6xl">
      <div className="mb-5 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <p className="kicker mb-2">{t('skills.detailKicker')}</p>
          <h1 className="serif text-3xl font-semibold tracking-tight text-foreground md:text-4xl">{detail.name}</h1>
          <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">{detail.description || t('skills.noDescription')}</p>
        </div>
        <Link to="/skills" className="inline-flex h-8 shrink-0 items-center gap-1.5 rounded-lg px-2.5 text-xs font-medium text-muted-foreground transition hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50">
          <ArrowLeftIcon className="size-3.5" />{t('skills.backToLibrary')}
        </Link>
      </div>

      <div className="mb-4 flex flex-wrap items-center gap-2">
        <Badge variant="accent">{detail.domainName}</Badge>
        <Badge variant="outline" className="font-mono">{detail.slug}</Badge>
        <Badge variant="outline">{t('skills.version', { version: detail.version })}</Badge>
        <Badge variant="outline">{t('skills.readOnly')}</Badge>
      </div>

      {error && <Alert variant="destructive" className="mb-4"><AlertDescription>{error}</AlertDescription></Alert>}

      <Card className="overflow-hidden">
        <CardHeader className="border-b border-border/70">
          <CardTitle className="flex items-center justify-between gap-3 text-base">
            <span>{t('skills.packageTree')}</span>
            <span className="font-mono text-[10px] font-normal text-muted-foreground">{detail.manifest.length} {t('skills.files')}</span>
          </CardTitle>
        </CardHeader>
        <CardContent className="grid gap-4 p-0 lg:grid-cols-[240px_minmax(0,1fr)]">
          <div className="border-b border-border/70 bg-muted/15 p-3 lg:border-b-0 lg:border-r" role="tree" aria-label={t('skills.packageTree')}>
            <div className="space-y-1">
              {folders.map((folder) => (
                <div key={folder} className="flex items-center gap-2 rounded-md px-2 py-1.5 text-xs text-muted-foreground" role="treeitem" aria-disabled="true">
                  <FolderIcon className="size-3.5 shrink-0" />
                  <span className="truncate font-mono">{folder}</span>
                </div>
              ))}
              {files.map((file) => (
                <button
                  key={file.path}
                  type="button"
                  className={`flex w-full items-center gap-2 rounded-md px-2 py-1.5 text-left text-xs transition ${activePath === file.path ? 'bg-primary/10 text-primary' : 'text-muted-foreground hover:bg-muted'}`}
                  onClick={() => setActivePath(file.path)}
                  role="treeitem"
                  aria-selected={activePath === file.path}
                >
                  <FileIcon className="size-3.5 shrink-0" />
                  <span className="truncate font-mono">{file.path}</span>
                </button>
              ))}
            </div>
          </div>

          <div className="min-h-[420px] p-4">
            {activeInfo ? (
              <>
                <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                  <div className="flex min-w-0 items-center gap-2">
                    <span className="truncate font-mono text-xs text-muted-foreground">{activeInfo.path}</span>
                    <SkillLanguageBadge path={activeInfo.path} content={fileContent} />
                  </div>
                  <Button size="sm" variant="outline" onClick={() => void downloadActive()}>
                    <DownloadIcon data-icon="inline-start" className="size-3.5" />
                    {t('skills.downloadFile')}
                  </Button>
                </div>
                {loadingFile ? (
                  <div className="flex min-h-[320px] items-center justify-center text-xs text-muted-foreground" aria-busy="true">
                    <LoaderCircleIcon className="mr-2 size-4 animate-spin" />{t('skills.loadingFile')}
                  </div>
                ) : activeInfo.binary ? (
                  <div className="flex min-h-[320px] items-center justify-center rounded-xl border border-dashed border-border/70 text-sm text-muted-foreground">{t('skills.binaryFileNote')}</div>
                ) : (
                  <SkillFilePreview path={activeInfo.path} content={fileContent} />
                )}
              </>
            ) : (
              <div className="flex min-h-[320px] items-center justify-center text-sm text-muted-foreground">{t('skills.emptyPackage')}</div>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
