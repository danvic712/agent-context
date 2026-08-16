import { useCallback, useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  DownloadIcon,
  FileCodeIcon,
  FileTextIcon,
  PackageOpenIcon,
  PencilIcon,
  SaveIcon,
  TrashIcon,
  UploadIcon,
} from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { MarkdownView } from '@/components/markdown-view'
import {
  deleteSkillFile,
  importSkillZip,
  readSkillFile,
  uploadSkillFiles,
  writeSkillFile,
  type SkillDetail,
} from '@/lib/api'
import { cn } from '@/lib/utils'

interface SkillPackageViewProps {
  detail: SkillDetail
  onChanged: (detail: SkillDetail) => void
  onDeleted: () => void
  onPublish: (skill: SkillDetail) => void
}

const isMarkdown = (path: string) => path.toLowerCase().endsWith('.md')
const extOf = (path: string) => (path.includes('.') ? path.split('.').pop() : 'txt')

function FileIconFor({ path }: { path: string }) {
  if (isMarkdown(path)) return <FileTextIcon className="size-3.5 text-[#5e6ad2]" />
  const ext = extOf(path)
  const color =
    ext === 'ts' || ext === 'tsx' || ext === 'js' ? '#37b679' : ext === 'sql' ? '#d97706' : ext === 'svg' ? '#a855f7' : ''
  return <FileCodeIcon className="size-3.5" style={color ? { color } : undefined} />
}

export function SkillPackageView({ detail, onChanged, onDeleted, onPublish }: SkillPackageViewProps) {
  const { t } = useTranslation()
  const [activeFile, setActiveFile] = useState<string>('SKILL.md')
  const [fileContent, setFileContent] = useState<string>('')
  const [loadingFile, setLoadingFile] = useState(true)
  const [editing, setEditing] = useState(false)
  const [saving, setSaving] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const zipInputRef = useRef<HTMLInputElement>(null)
  const dragDepth = useRef(0)

  // Keep the active file valid when the manifest changes.
  useEffect(() => {
    if (!detail.manifest.some((f) => f.path === activeFile)) {
      const main = detail.manifest.find((f) => f.path === 'SKILL.md')
      setActiveFile(main ? main.path : detail.manifest[0]?.path ?? '')
    }
  }, [detail, activeFile])

  const loadFile = useCallback(
    async (path: string) => {
      const info = detail.manifest.find((f) => f.path === path)
      if (!info) return
      setLoadingFile(true)
      setError(null)
      try {
        const blob = await readSkillFile(detail.id, path)
        if (!info.binary) {
          setFileContent(await blob.text())
        }
      } catch (cause) {
        setError(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'))
      } finally {
        setLoadingFile(false)
      }
    },
    [detail, t],
  )

  useEffect(() => {
    if (activeFile) void loadFile(activeFile)
  }, [activeFile, loadFile])

  const saveFile = async () => {
    setSaving(true)
    setError(null)
    setNotice(null)
    try {
      const updated = await writeSkillFile(detail.id, activeFile, fileContent)
      onChanged(updated)
      setEditing(false)
      setNotice(t('skills.fileSaved'))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.fileSaveFailed'))
    } finally {
      setSaving(false)
    }
  }

  const removeFile = async (path: string) => {
    if (!window.confirm(t('skills.deleteFileConfirm', { name: path }))) return
    setError(null)
    try {
      const updated = await deleteSkillFile(detail.id, path)
      onChanged(updated)
      if (activeFile === path) setActiveFile('SKILL.md')
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.fileSaveFailed'))
    }
  }

  const handleFiles = async (files: FileList | null) => {
    if (!files || files.length === 0) return
    setUploading(true)
    setError(null)
    setNotice(null)
    try {
      const updated = await uploadSkillFiles(detail.id, Array.from(files))
      onChanged(updated)
      setNotice(t('skills.uploadedFiles', { count: files.length }))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.uploadFailed'))
    } finally {
      setUploading(false)
    }
  }

  const handleImport = async (file: File | null) => {
    if (!file) return
    setUploading(true)
    setError(null)
    setNotice(null)
    try {
      const updated = await importSkillZip(detail.id, file)
      onChanged(updated)
      setNotice(t('skills.uploadedFiles', { count: updated.manifest.length }))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.importFailed'))
    } finally {
      setUploading(false)
    }
  }

  const downloadActive = async () => {
    const blob = await readSkillFile(detail.id, activeFile)
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = activeFile.split('/').pop() ?? activeFile
    a.click()
    URL.revokeObjectURL(url)
  }

  const activeInfo = detail.manifest.find((f) => f.path === activeFile)

  return (
    <Card className="overflow-hidden">
      {/* Detail head */}
      <CardHeader className="flex flex-row flex-wrap items-center gap-3 border-b border-border px-4 py-3">
        <div className="min-w-0">
          <CardTitle className="text-[15px] font-semibold tracking-tight">{detail.name}</CardTitle>
          <p className="mt-0.5 truncate font-mono text-[11.5px] text-muted-foreground">
            {detail.domainName} / {detail.slug}
          </p>
        </div>
        <div className="ml-auto flex items-center gap-2">
          <Badge variant="accent" className="hidden sm:inline-flex">
            {detail.domainName}
          </Badge>
          <Badge variant="outline">
            {t('skills.version', { version: detail.version })} · {t('skills.package')}
          </Badge>
          <Button size="sm" variant="outline" onClick={() => onPublish(detail)}>
            {t('skills.publishVersion')}
          </Button>
          <Button size="sm" variant="outline" onClick={() => zipInputRef.current?.click()}>
            <PackageOpenIcon data-icon="inline-start" className="size-4" />
            {t('skills.importPackage')}
          </Button>
          <Button
            size="sm"
            variant="destructive"
            onClick={() => {
              if (window.confirm(t('skills.deleteConfirm', { slug: detail.slug }))) onDeleted()
            }}
          >
            <TrashIcon data-icon="inline-start" className="size-4" />
            {t('common.delete')}
          </Button>
          <input
            ref={zipInputRef}
            type="file"
            accept=".zip"
            className="hidden"
            onChange={(e) => {
              void handleImport(e.target.files?.[0] ?? null)
              e.target.value = ''
            }}
          />
        </div>
      </CardHeader>

      {/* File tabs */}
      <div className="flex gap-0.5 overflow-x-auto border-b border-border px-3 pt-2">
        {detail.manifest.length === 0 ? (
          <span className="pb-2 text-xs text-muted-foreground">{t('skills.emptyPackage')}</span>
        ) : (
          detail.manifest.map((file) => (
            <button
              key={file.path}
              type="button"
              onClick={() => setActiveFile(file.path)}
              className={cn(
                'flex items-center gap-1.5 rounded-t-md border border-transparent px-2.5 py-1.5 font-mono text-[12px] text-muted-foreground transition-colors',
                activeFile === file.path &&
                  'border-border border-b-transparent bg-secondary text-foreground',
              )}
            >
              <FileIconFor path={file.path} />
              {file.path}
            </button>
          ))
        )}
      </div>

      {/* Body */}
      <CardContent className="p-0">
        {/* Drag & drop upload zone */}
        <div
          onDragEnter={(e) => {
            e.preventDefault()
            dragDepth.current++
          }}
          onDragLeave={(e) => {
            e.preventDefault()
            dragDepth.current = Math.max(0, dragDepth.current - 1)
          }}
          onDragOver={(e) => e.preventDefault()}
          onDrop={(e) => {
            e.preventDefault()
            dragDepth.current = 0
            void handleFiles(e.dataTransfer.files)
          }}
        >
          {error && (
            <Alert variant="destructive" className="m-4">
              <AlertTitle>{t('skills.uploadFailed')}</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
          {notice && (
            <Alert className="m-4">
              <AlertDescription>{notice}</AlertDescription>
            </Alert>
          )}

          <div className="p-4">
            {/* Active file header */}
            {activeFile && (
              <div className="mb-3 flex items-center justify-between gap-3">
                <span className="font-mono text-[12px] text-muted-foreground">{activeFile}</span>
                <div className="flex items-center gap-2">
                  {activeInfo?.binary ? (
                    <>
                      <span className="text-xs text-muted-foreground">{t('skills.binaryFileNote')}</span>
                      <Button size="sm" variant="outline" onClick={() => void downloadActive()}>
                        <DownloadIcon data-icon="inline-start" className="size-4" />
                        {t('skills.downloadFile')}
                      </Button>
                    </>
                  ) : (
                    <>
                      {isMarkdown(activeFile) && !editing && (
                        <Button size="sm" variant="outline" onClick={() => setEditing(true)}>
                          <PencilIcon data-icon="inline-start" className="size-4" />
                          {t('skills.edit')}
                        </Button>
                      )}
                      {editing && (
                        <>
                          <Button size="sm" variant="ghost" onClick={() => setEditing(false)}>
                            {t('skills.preview')}
                          </Button>
                          <Button size="sm" onClick={() => void saveFile()} disabled={saving}>
                            <SaveIcon data-icon="inline-start" className="size-4" />
                            {t('common.save')}
                          </Button>
                        </>
                      )}
                      <Button
                        size="sm"
                        variant="destructive"
                        onClick={() => void removeFile(activeFile)}
                        aria-label={t('skills.deleteAria', { slug: activeFile })}
                      >
                        <TrashIcon data-icon="inline-start" className="size-4" />
                      </Button>
                    </>
                  )}
                </div>
              </div>
            )}

            {/* File content */}
            {loadingFile ? (
              <div className="flex flex-col gap-2" aria-busy="true">
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-5/6" />
                <Skeleton className="h-4 w-2/3" />
                <Skeleton className="h-4 w-4/6" />
              </div>
            ) : activeFile && activeInfo && !activeInfo.binary ? (
              editing ? (
                <textarea
                  value={fileContent}
                  onChange={(e) => setFileContent(e.target.value)}
                  rows={18}
                  spellCheck={false}
                  className="w-full resize-y rounded-lg border border-input bg-background p-3 font-mono text-[12.5px] leading-relaxed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                />
              ) : isMarkdown(activeFile) ? (
                <MarkdownView content={fileContent} />
              ) : (
                <pre className="overflow-x-auto rounded-lg border border-border bg-panel2 p-3 font-mono text-[12.5px] leading-relaxed text-foreground">
                  {fileContent}
                </pre>
              )
            ) : (
              <p className="text-sm text-muted-foreground">{t('skills.emptyPackage')}</p>
            )}
          </div>

          {/* Upload footer */}
          <div className="flex items-center gap-3 border-t border-border px-4 py-3">
            <Button
              size="sm"
              variant="outline"
              disabled={uploading}
              onClick={() => fileInputRef.current?.click()}
            >
              <UploadIcon data-icon="inline-start" className="size-4" />
              {uploading ? t('skills.uploading') : t('skills.uploadFiles')}
            </Button>
            <span className="text-xs text-muted-foreground">{t('skills.dragHint')}</span>
            <span className="ml-auto text-xs text-muted-foreground">
              {detail.manifest.length} {t('skills.files')}
            </span>
            <input
              ref={fileInputRef}
              type="file"
              multiple
              className="hidden"
              onChange={(e) => {
                void handleFiles(e.target.files)
                e.target.value = ''
              }}
            />
          </div>
        </div>
      </CardContent>
    </Card>
  )
}
