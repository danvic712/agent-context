import { useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  AlertCircleIcon,
  ArrowLeftIcon,
  ChevronDownIcon,
  DownloadIcon,
  FileIcon,
  FolderIcon,
  LoaderCircleIcon,
  SparklesIcon,
} from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ActionBar, ActionBarStatus } from '@/components/ui/action-bar'
import { useActionFeedback } from '@/components/ui/action-feedback'
import { Badge } from '@/components/ui/badge'
import { Button, buttonVariants } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { SkillFilePreview } from './skill-file-preview'
import { SkillLanguageBadge } from './skill-language-badge'
import { getSkillById, readSkillFile, type SkillDetail, type SkillFileInfo } from '@/lib/api'
import { skillSourceKey } from './skill-source'

const fileName = (path: string) => path.split('/').pop() ?? path

const sortFiles = (files: SkillFileInfo[]) => [...files].sort((a, b) =>
  fileName(a.path).localeCompare(fileName(b.path), undefined, { numeric: true, sensitivity: 'base' })
    || a.path.localeCompare(b.path, undefined, { numeric: true, sensitivity: 'base' }),
)

type FileTreeFolder = {
  kind: 'folder'
  name: string
  path: string
  children: FileTreeNode[]
}

type FileTreeFile = {
  kind: 'file'
  name: string
  path: string
  info: SkillFileInfo
}

type FileTreeNode = FileTreeFolder | FileTreeFile

const isFolder = (node: FileTreeNode): node is FileTreeFolder => node.kind === 'folder'

const sortTreeNodes = (nodes: FileTreeNode[]) => [...nodes].sort((a, b) => {
  if (a.kind !== b.kind) return a.kind === 'folder' ? -1 : 1
  return a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' })
})

const buildFileTree = (files: SkillFileInfo[], folders: string[] = []): FileTreeNode[] => {
  const root: FileTreeFolder = { kind: 'folder', name: '', path: '', children: [] }

  const ensureFolder = (parts: string[]) => {
    let parent = root
    for (let index = 0; index < parts.length; index += 1) {
      const path = parts.slice(0, index + 1).join('/')
      let folder = parent.children.find(
        (node): node is FileTreeFolder => isFolder(node) && node.path === path,
      )
      if (!folder) {
        folder = { kind: 'folder', name: parts[index], path, children: [] }
        parent.children.push(folder)
      }
      parent = folder
    }
    return parent
  }

  folders.forEach((path) => ensureFolder(path.split('/').filter(Boolean)))

  for (const info of files) {
    const parts = info.path.split('/').filter(Boolean)
    if (parts.length === 0) continue

    const parent = ensureFolder(parts.slice(0, -1))
    parent.children.push({
      kind: 'file',
      name: parts[parts.length - 1],
      path: info.path,
      info,
    })
  }

  const sortBranch = (branch: FileTreeFolder) => {
    branch.children = sortTreeNodes(branch.children)
    branch.children.filter(isFolder).forEach(sortBranch)
  }
  sortBranch(root)
  return root.children
}

function DetailLoadingState() {
  const { t } = useTranslation()

  return (
    <PageFrame>
      <div className="skill-detail-loading" aria-busy="true" aria-label={t('skills.loadingDetail')}>
        <div className="skill-detail-loading__header">
          <Skeleton className="h-3 w-32" />
          <Skeleton className="h-14 w-[min(30rem,80%)]" />
          <Skeleton className="h-4 w-[min(38rem,90%)]" />
        </div>
        <Card className="overflow-hidden">
          <CardContent className="grid gap-0 p-0 lg:grid-cols-[minmax(220px,0.36fr)_minmax(0,1fr)]">
            <div className="space-y-3 border-b border-border/70 p-5 lg:border-b-0 lg:border-r">
              <Skeleton className="h-4 w-28" />
              {Array.from({ length: 5 }, (_, index) => (
                <Skeleton key={index} className="h-8 w-full" />
              ))}
            </div>
            <div className="space-y-5 p-5">
              <Skeleton className="h-8 w-2/3" />
              <Skeleton className="h-[320px] w-full rounded-xl" />
            </div>
          </CardContent>
        </Card>
      </div>
    </PageFrame>
  )
}

function MissingDetailState({ error }: { error: string }) {
  const { t } = useTranslation()

  return (
    <PageFrame>
      <Card className="skill-detail-state">
        <CardContent className="flex flex-col items-center px-6 py-16 text-center">
          <div className="skill-detail-state__icon" aria-hidden="true">
            <AlertCircleIcon className="size-5" />
          </div>
          <Alert variant="destructive" className="mt-5 max-w-xl text-left">
            <AlertCircleIcon className="size-4" />
            <AlertTitle>{t('skills.detailLoadFailed')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
          <Link className={cn(buttonVariants({ variant: 'outline', size: 'sm' }), 'mt-6 no-underline')} to="/skills">
            <ArrowLeftIcon data-icon="inline-start" className="size-3.5" />
            {t('skills.backToLibrary')}
          </Link>
        </CardContent>
      </Card>
    </PageFrame>
  )
}

function FileTreeNodeView({
  node,
  activePath,
  onSelect,
}: {
  node: FileTreeNode
  activePath: string
  onSelect: (path: string) => void
}) {
  if (node.kind === 'folder') {
    return (
      <details className="skill-file-tree__folder" open>
        <summary className="skill-file-tree__folder-row" role="treeitem">
          <ChevronDownIcon className="skill-file-tree__chevron size-3.5" aria-hidden="true" />
          <FolderIcon className="size-3.5 shrink-0 text-[var(--hi)]" aria-hidden="true" />
          <span className="skill-file-tree__name" title={node.path}>{node.name}</span>
        </summary>
        <div className="skill-file-tree__children" role="group">
          {node.children.map((child) => (
            <FileTreeNodeView key={child.path} node={child} activePath={activePath} onSelect={onSelect} />
          ))}
        </div>
      </details>
    )
  }

  return (
    <button
      type="button"
      className="skill-file-tree__file-row"
      data-selected={activePath === node.path || undefined}
      onClick={() => onSelect(node.path)}
      role="treeitem"
      aria-selected={activePath === node.path}
      title={node.path}
    >
      <FileIcon className="size-3.5 shrink-0" aria-hidden="true" />
      <span className="skill-file-tree__name">{node.name}</span>
      {node.info.binary && <span className="skill-file-tree__binary">BIN</span>}
    </button>
  )
}

function MetadataItem({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="skill-detail-metadata__item">
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  )
}

export function SkillDetailPage() {
  const { t } = useTranslation()
  const { push } = useActionFeedback()
  const { id } = useParams<{ id: string }>()
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [activePath, setActivePath] = useState('')
  const [fileContent, setFileContent] = useState('')
  const [loading, setLoading] = useState(true)
  const [loadingFile, setLoadingFile] = useState(false)
  const [downloading, setDownloading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!id) {
      setError(t('skills.detailLoadFailed'))
      setLoading(false)
      return
    }

    let active = true
    setLoading(true)
    setDetail(null)
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

  const files = useMemo(() => sortFiles(detail?.manifest ?? []), [detail])
  const fileTree = useMemo(() => buildFileTree(files, detail?.folders ?? []), [detail?.folders, files])
  const activeInfo = files.find((file) => file.path === activePath)
  const sourceLabel = detail ? t(skillSourceKey(detail.sourceType)) : ''

  useEffect(() => {
    if (!detail || !activeInfo) return
    let active = true
    setLoadingFile(true)
    setFileContent('')
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
    if (!detail || !activeInfo || downloading) return
    setDownloading(true)
    try {
      const blob = await readSkillFile(detail.id, activeInfo.path)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = fileName(activeInfo.path)
      anchor.click()
      URL.revokeObjectURL(url)
      push(t('skills.fileDownloaded', { name: fileName(activeInfo.path) }), 'success')
    } catch (cause) {
      push(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'), 'error')
    } finally {
      setDownloading(false)
    }
  }

  const goBackToLibrary = () => {
    push(t('skills.backToLibraryFeedback'), 'info')
  }

  const useSkill = () => {
    if (detail) push(t('skills.skillAddedToContext', { name: detail.name }), 'success')
  }

  if (loading) return <DetailLoadingState />
  if (error && !detail) return <MissingDetailState error={error} />
  if (!detail) return <MissingDetailState error={t('skills.detailLoadFailed')} />

  return (
    <PageFrame
      header={(
        <PageHeader
          className="skill-detail-header"
          eyebrow={t('skills.detailKicker')}
          title={detail.name}
          description={detail.description || t('skills.noDescription')}
          actions={(
            <Link to="/skills" className="ui-inline-action" onClick={goBackToLibrary}>
              <ArrowLeftIcon className="size-3.5" />
              {t('skills.backToLibrary')}
            </Link>
          )}
        />
      )}
    >
      <section className="skill-detail-page" aria-label={detail.name}>
        <div className="skill-detail-summary">
          <div className="skill-detail-summary__status">
            <Badge variant="accent">
              <span className="size-1.5 rounded-full bg-ok" aria-hidden="true" />
              {t('skills.statusInstalled')}
            </Badge>
            <Badge variant="outline">{t('skills.readOnly')}</Badge>
            <Badge variant="outline">{sourceLabel}</Badge>
          </div>
          <dl className="skill-detail-metadata">
            <MetadataItem label={t('skills.domain')}>
              <span title={detail.domainName}>{detail.domainName}</span>
            </MetadataItem>
            <MetadataItem label={t('skills.slug')}>
              <code title={detail.slug}>{detail.slug}</code>
            </MetadataItem>
            <MetadataItem label={t('skills.detailVersion')}>
              <span>{t('skills.version', { version: detail.version })}</span>
            </MetadataItem>
            <MetadataItem label={t('skills.detailSource')}>
              <span title={sourceLabel}>{sourceLabel}</span>
            </MetadataItem>
          </dl>
        </div>

        {error && (
          <Alert variant="destructive" className="mb-4" role="alert">
            <AlertCircleIcon className="size-4" />
            <AlertTitle>{t('skills.fileLoadFailed')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        <section className="skill-detail-workspace ui-surface" aria-label={t('skills.packageTree')}>
          <header className="skill-detail-workspace__header">
            <div className="min-w-0">
              <p className="skill-detail-workspace__eyebrow">{t('skills.packageTree')}</p>
              <h2>{t('skills.packageFilesTitle')}</h2>
              <p>{t('skills.packageFilesDescription')}</p>
            </div>
            <span className="skill-detail-workspace__count">
              {t('skills.fileCount', { count: detail.manifest.length })}
            </span>
          </header>

          <div className="skill-detail-workspace__grid">
            <aside className="skill-file-browser">
              <details className="skill-file-browser__disclosure" open>
                <summary className="skill-file-browser__summary">
                  <span className="min-w-0">
                    <span className="skill-file-browser__label">{t('skills.fileNavigation')}</span>
                    <strong>{t('skills.packageTree')}</strong>
                  </span>
                  <span className="skill-file-browser__summary-meta">
                    {detail.manifest.length} {t('skills.files')}
                    <ChevronDownIcon className="skill-file-browser__summary-icon size-4" aria-hidden="true" />
                  </span>
                </summary>
                <div className="skill-file-tree" role="tree" aria-label={t('skills.packageTree')}>
                  {fileTree.length > 0 ? fileTree.map((node) => (
                    <FileTreeNodeView key={node.path} node={node} activePath={activePath} onSelect={setActivePath} />
                  )) : (
                    <p className="skill-detail-empty">{t('skills.emptyPackage')}</p>
                  )}
                </div>
              </details>
            </aside>

            <section className="skill-detail-preview" aria-label={t('skills.preview')}>
              {activeInfo ? (
                <>
                  <header className="skill-detail-preview__header">
                    <div className="skill-detail-preview__file">
                      <div className="skill-detail-preview__file-title">
                        <FileIcon className="size-4 shrink-0 text-[var(--hi)]" aria-hidden="true" />
                        <h3 title={activeInfo.path}>{fileName(activeInfo.path)}</h3>
                      </div>
                      <p className="skill-detail-preview__path" title={activeInfo.path}>{activeInfo.path}</p>
                    </div>
                    <div className="skill-detail-preview__tools">
                      <SkillLanguageBadge path={activeInfo.path} content={fileContent} />
                      <Button type="button" size="sm" variant="outline" onClick={() => void downloadActive()} disabled={downloading}>
                        {downloading
                          ? <LoaderCircleIcon data-icon="inline-start" className="size-3.5 animate-spin" />
                          : <DownloadIcon data-icon="inline-start" className="size-3.5" />}
                        {t('skills.downloadFile')}
                      </Button>
                    </div>
                  </header>
                  <div className="skill-detail-preview__body">
                    {loadingFile ? (
                      <div className="skill-detail-preview__loading" aria-busy="true">
                        <LoaderCircleIcon className="size-4 animate-spin" />
                        {t('skills.loadingFile')}
                      </div>
                    ) : activeInfo.binary ? (
                      <div className="skill-detail-preview__binary">
                        <FileIcon className="size-6" aria-hidden="true" />
                        <p>{t('skills.binaryFileNote')}</p>
                        <span>{t('skills.binaryFileDownloadHint')}</span>
                      </div>
                    ) : (
                      <SkillFilePreview path={activeInfo.path} content={fileContent} />
                    )}
                  </div>
                </>
              ) : (
                <div className="skill-detail-preview__empty">
                  <FileIcon className="size-6" aria-hidden="true" />
                  <span>{t('skills.emptyPackage')}</span>
                </div>
              )}
            </section>
          </div>
        </section>

        <ActionBar
          sticky
          className="skill-detail-actions"
          status={(
            <ActionBarStatus>
              <span>{t('skills.readOnlyPackageStatus', { version: detail.version })}</span>
            </ActionBarStatus>
          )}
        >
          <Link
            to="/skills"
            className={cn(buttonVariants({ variant: 'outline', size: 'sm' }), 'no-underline')}
            onClick={goBackToLibrary}
          >
            <ArrowLeftIcon data-icon="inline-start" className="size-3.5" />
            {t('skills.backToLibrary')}
          </Link>
          <Button type="button" size="sm" onClick={useSkill}>
            <SparklesIcon data-icon="inline-start" className="size-3.5" />
            {t('skills.useSkill')}
          </Button>
        </ActionBar>
      </section>
    </PageFrame>
  )
}
