import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { AlertCircleIcon, PanelRightCloseIcon, PanelRightOpenIcon, SearchXIcon, Trash2Icon, WrenchIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useActionFeedback } from '@/components/ui/action-feedback'
import { PageFrame } from '@/components/ui/page-frame'
import {
  deleteSkill,
  downloadSkillPackage,
  getSkillById,
  listSkills,
  readSkillFile,
  type SkillDetail,
  type SkillFileInfo,
  type SkillItem,
} from '@/lib/api'
import { cn } from '@/lib/utils'
import { SkillDetailActions } from './skill-detail/skill-detail-actions'
import { SkillDetailMetadata } from './skill-detail/skill-detail-metadata'
import { SkillDetailPackageFiles } from './skill-detail/skill-detail-package-files'
import { SkillDetailReader } from './skill-detail/skill-detail-reader'
import { buildFileTree, fileName, sortFiles } from './skill-detail/file-tree'
import { skillSourceKey } from './skill-source'
import { SkillLibraryList } from './skill-library-list'
import { SkillPageHeader } from './skill-page-header'
import { SkillSearchFilters, type InstalledSkillFilters } from './skill-search-filters'
import './skills.css'
import './skill-detail/skill-detail.css'

const PAGE_SIZE = 20
const PACKAGE_DOWNLOAD_KEY = '__package__'
const DEFAULT_FILTERS: InstalledSkillFilters = {
  search: '',
  domain: '',
  sourceType: '',
  sort: 'updated-desc',
}

interface SkillsNavigationState {
  highlightId?: string
  successSlug?: string
}

function SkillLibraryDetailSkeleton() {
  const { t } = useTranslation()

  return (
    <div className="skill-library-detail-skeleton" aria-busy="true" aria-label={t('skills.loadingDetail')}>
      <div className="skill-library-detail-skeleton__heading">
        <Skeleton className="h-3 w-32" />
        <Skeleton className="h-9 w-2/3" />
        <Skeleton className="h-4 w-full" />
      </div>
      <div className="skill-detail-reader ui-surface overflow-hidden">
        <div className="flex items-center gap-3 border-b border-border/70 p-4">
          <Skeleton className="size-8 rounded-xl" />
          <div className="min-w-0 flex-1 space-y-2">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="h-2.5 w-28" />
          </div>
        </div>
        <div className="space-y-4 p-6 sm:p-10">
          <Skeleton className="h-7 w-1/2" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-5/6" />
          <Skeleton className="h-4 w-4/6" />
          <Skeleton className="mt-8 h-32 w-full rounded-xl" />
        </div>
      </div>
    </div>
  )
}

function SkillLibraryNoSelection() {
  const { t } = useTranslation()

  return (
    <div className="skill-library-no-selection ui-surface">
      <SearchXIcon className="size-6" aria-hidden="true" />
      <h2>{t('skills.noSkillsHint')}</h2>
      <p>{t('skills.emptyDescription')}</p>
    </div>
  )
}

export function SkillsLibraryPage() {
  const { t } = useTranslation()
  const { push } = useActionFeedback()
  const navigate = useNavigate()
  const location = useLocation()
  const navigationState = location.state as SkillsNavigationState | null
  const navigationHighlightRef = useRef<string | null>(navigationState?.highlightId ?? null)
  const sentinelRef = useRef<HTMLDivElement | null>(null)
  const requestRef = useRef<AbortController | null>(null)
  const paginationRequestRef = useRef<AbortController | null>(null)
  const requestVersionRef = useRef(0)
  const detailRequestVersionRef = useRef(0)
  const [items, setItems] = useState<SkillItem[]>([])
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [hasMore, setHasMore] = useState(false)
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [errorScope, setErrorScope] = useState<'initial' | 'more' | null>(null)
  const [draftFilters, setDraftFilters] = useState<InstalledSkillFilters>(DEFAULT_FILTERS)
  const [appliedFilters, setAppliedFilters] = useState<InstalledSkillFilters>(DEFAULT_FILTERS)
  const [highlightedId, setHighlightedId] = useState<string | null>(navigationState?.highlightId ?? null)
  const [selectedId, setSelectedId] = useState<string | null>(navigationState?.highlightId ?? null)
  const [notice, setNotice] = useState<string | null>(navigationState?.successSlug ? t('skills.uploadSuccess', { slug: navigationState.successSlug }) : null)
  const [metaCollapsed, setMetaCollapsed] = useState(true)
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [loadingDetail, setLoadingDetail] = useState(false)
  const [selectedSupportPath, setSelectedSupportPath] = useState('')
  const [mainContent, setMainContent] = useState('')
  const [supportContent, setSupportContent] = useState('')
  const [loadingMain, setLoadingMain] = useState(false)
  const [loadingSupport, setLoadingSupport] = useState(false)
  const [downloadingPath, setDownloadingPath] = useState('')
  const [deletingDetail, setDeletingDetail] = useState(false)
  const [detailError, setDetailError] = useState<string | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)

  const selectedIsLoaded = Boolean(selectedId && items.some((item) => item.id === selectedId))
  const highlightedSelectionId = navigationHighlightRef.current && items.some((item) => item.id === navigationHighlightRef.current)
    ? navigationHighlightRef.current
    : null
  const activeId = selectedIsLoaded ? selectedId : highlightedSelectionId ?? items[0]?.id ?? null
  const detailId = loading ? null : activeId

  const selectSkill = useCallback((id: string) => {
    setSelectedId(id)
  }, [])

  const clearSelection = useCallback(() => {
    setSelectedId(null)
  }, [])

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      const nextFilters = {
        ...draftFilters,
        search: draftFilters.search.trim(),
        domain: draftFilters.domain.trim(),
      }
      setAppliedFilters((current) => (
        current.search === nextFilters.search
          && current.domain === nextFilters.domain
          && current.sourceType === nextFilters.sourceType
          && current.sort === nextFilters.sort
          ? current
          : nextFilters
      ))
    }, 250)
    return () => window.clearTimeout(timeout)
  }, [draftFilters])

  const loadInitial = useCallback(async (filters: InstalledSkillFilters) => {
    const requestVersion = ++requestVersionRef.current
    requestRef.current?.abort()
    paginationRequestRef.current?.abort()
    const controller = new AbortController()
    requestRef.current = controller
    setLoading(true)
    setLoadingMore(false)
    setItems([])
    setNextCursor(null)
    setHasMore(false)
    setError(null)
    setErrorScope(null)
    try {
      const page = await listSkills(PAGE_SIZE, null, controller.signal, {
        search: filters.search || undefined,
        domain: filters.domain || undefined,
        sourceType: filters.sourceType || undefined,
        sort: filters.sort,
      })
      if (requestVersion !== requestVersionRef.current) return
      setItems(page.items)
      setNextCursor(page.nextCursor)
      setHasMore(page.hasMore)
    } catch (cause) {
      if (!controller.signal.aborted && requestVersion === requestVersionRef.current) {
        setError(cause instanceof Error ? cause.message : t('skills.failedLoad'))
        setErrorScope('initial')
      }
    } finally {
      if (!controller.signal.aborted && requestVersion === requestVersionRef.current) setLoading(false)
    }
  }, [t])

  const loadMore = useCallback(async () => {
    if (!hasMore || loadingMore || !nextCursor) return
    const requestVersion = requestVersionRef.current
    const controller = new AbortController()
    paginationRequestRef.current?.abort()
    paginationRequestRef.current = controller
    setLoadingMore(true)
    setError(null)
    setErrorScope(null)
    try {
      const page = await listSkills(PAGE_SIZE, nextCursor, controller.signal, {
        search: appliedFilters.search || undefined,
        domain: appliedFilters.domain || undefined,
        sourceType: appliedFilters.sourceType || undefined,
        sort: appliedFilters.sort,
      })
      if (controller.signal.aborted || requestVersion !== requestVersionRef.current) return
      setItems((current) => {
        const known = new Set(current.map((item) => item.id))
        return [...current, ...page.items.filter((item) => !known.has(item.id))]
      })
      setNextCursor(page.nextCursor)
      setHasMore(page.hasMore)
    } catch (cause) {
      if (!controller.signal.aborted && requestVersion === requestVersionRef.current) {
        setError(cause instanceof Error ? cause.message : t('skills.failedLoadMore'))
        setErrorScope('more')
      }
    } finally {
      if (requestVersion === requestVersionRef.current) setLoadingMore(false)
    }
  }, [appliedFilters, hasMore, loadingMore, nextCursor, t])

  useEffect(() => {
    void loadInitial(appliedFilters)
    return () => {
      requestRef.current?.abort()
      paginationRequestRef.current?.abort()
    }
  }, [appliedFilters, loadInitial])

  useEffect(() => {
    const node = sentinelRef.current
    if (!node || loading || !hasMore) return
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) void loadMore()
      },
      { rootMargin: '240px 0px' },
    )
    observer.observe(node)
    return () => observer.disconnect()
  }, [hasMore, loadMore, loading])

  useEffect(() => {
    if (!navigationState?.highlightId && !navigationState?.successSlug) return
    navigate(location.pathname, { replace: true, state: null })
  }, [location.pathname, navigate, navigationState?.highlightId, navigationState?.successSlug])

  useEffect(() => {
    if (!highlightedId) return
    const timeout = window.setTimeout(() => setHighlightedId(null), 5000)
    return () => window.clearTimeout(timeout)
  }, [highlightedId])

  useEffect(() => {
    if (loading) return
    if (items.length === 0) {
      if (selectedId) clearSelection()
      return
    }
    if (activeId && selectedId !== activeId) selectSkill(activeId)
    if (highlightedSelectionId) navigationHighlightRef.current = null
  }, [activeId, clearSelection, highlightedSelectionId, items.length, loading, selectSkill, selectedId])

  useEffect(() => {
    if (!detailId) {
      setDetail(null)
      setLoadingDetail(false)
      setDetailError(null)
      return
    }

    const requestVersion = ++detailRequestVersionRef.current
    let active = true
    setLoadingDetail(true)
    setDetail(null)
    setSelectedSupportPath('')
    setMainContent('')
    setSupportContent('')
    setDetailError(null)
    setFileError(null)
    void getSkillById(detailId)
      .then((loaded) => {
        if (!active || requestVersion !== detailRequestVersionRef.current) return
        setDetail(loaded)
      })
      .catch((cause) => {
        if (active && requestVersion === detailRequestVersionRef.current) {
          setDetailError(cause instanceof Error ? cause.message : t('skills.detailLoadFailed'))
        }
      })
      .finally(() => {
        if (active && requestVersion === detailRequestVersionRef.current) setLoadingDetail(false)
      })
    return () => { active = false }
  }, [detailId, t])

  const files = useMemo(() => sortFiles(detail?.manifest ?? []), [detail?.manifest])
  const fileTree = useMemo(() => buildFileTree(files, detail?.folders ?? []), [detail?.folders, files])
  const mainFile = files.find((file) => file.path === 'SKILL.md')
  const selectedSupportFile = files.find((file) => file.path === selectedSupportPath && file.path !== 'SKILL.md')
  const sourceLabel = detail ? t(skillSourceKey(detail.sourceType)) : ''

  useEffect(() => {
    if (!detail || !mainFile) {
      setLoadingMain(false)
      setMainContent('')
      return
    }
    let active = true
    setLoadingMain(true)
    setMainContent('')
    setFileError(null)
    void readSkillFile(detail.id, mainFile.path)
      .then(async (blob) => {
        if (!active) return
        setMainContent(mainFile.binary ? '' : await blob.text())
      })
      .catch((cause) => {
        if (active) setFileError(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'))
      })
      .finally(() => {
        if (active) setLoadingMain(false)
      })
    return () => { active = false }
  }, [detail, mainFile, t])

  useEffect(() => {
    if (!detail || !selectedSupportFile) {
      setLoadingSupport(false)
      setSupportContent('')
      return
    }
    let active = true
    setLoadingSupport(true)
    setSupportContent('')
    setFileError(null)
    void readSkillFile(detail.id, selectedSupportFile.path)
      .then(async (blob) => {
        if (!active) return
        setSupportContent(selectedSupportFile.binary ? '' : await blob.text())
      })
      .catch((cause) => {
        if (active) setFileError(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'))
      })
      .finally(() => {
        if (active) setLoadingSupport(false)
      })
    return () => { active = false }
  }, [detail, selectedSupportFile, t])

  const refresh = async () => {
    setRefreshing(true)
    try {
      await loadInitial(appliedFilters)
    } finally {
      setRefreshing(false)
    }
  }

  const deleteCurrentSkill = async () => {
    if (!detail || deletingDetail || !window.confirm(t('skills.deleteConfirm', { slug: detail.slug }))) return

    setDeletingDetail(true)
    try {
      await deleteSkill(detail.id)
      clearSelection()
      await loadInitial(appliedFilters)
      push(t('skills.deleteSuccess', { slug: detail.slug }), 'success')
    } catch (cause) {
      push(cause instanceof Error ? cause.message : t('skills.failedDelete'), 'error')
    } finally {
      setDeletingDetail(false)
    }
  }

  const downloadFile = async (file: SkillFileInfo | undefined) => {
    if (!detail || !file || downloadingPath) return
    setDownloadingPath(file.path)
    try {
      const blob = await readSkillFile(detail.id, file.path)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = fileName(file.path)
      anchor.click()
      URL.revokeObjectURL(url)
      push(t('skills.fileDownloaded', { name: fileName(file.path) }), 'success')
    } catch (cause) {
      push(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'), 'error')
    } finally {
      setDownloadingPath('')
    }
  }

  const downloadMainPackage = async () => {
    if (!detail || !mainFile || downloadingPath) return
    setDownloadingPath(PACKAGE_DOWNLOAD_KEY)
    try {
      const blob = detail.sourceType === 'zip'
        ? await downloadSkillPackage(detail.id)
        : await readSkillFile(detail.id, mainFile.path)
      const name = detail.sourceType === 'zip'
        ? `${detail.slug}-v${detail.version}.zip`
        : fileName(mainFile.path)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = name
      anchor.click()
      URL.revokeObjectURL(url)
      push(t('skills.fileDownloaded', { name }), 'success')
    } catch (cause) {
      push(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'), 'error')
    } finally {
      setDownloadingPath('')
    }
  }

  const filterActive = Boolean(appliedFilters.search || appliedFilters.domain || appliedFilters.sourceType)
  const domainOptions = useMemo(
    () => Array.from(new Set(items.map((item) => item.domainName.trim()).filter(Boolean))).sort((left, right) => left.localeCompare(right)),
    [items],
  )

  return (
    <PageFrame
      header={(
        <SkillPageHeader items={items} loading={loading} hasMore={hasMore} onUpload={() => navigate('/skills/upload')} />
      )}
    >
      {notice && (
        <div className="skill-library-notice" role="status">
          <span className="skill-library-notice__mark" aria-hidden="true">✓</span>
          <div className="min-w-0 flex-1">{notice}</div>
          <button type="button" className="skill-library-notice__dismiss" aria-label={t('skills.dismissNotice')} onClick={() => setNotice(null)}>×</button>
        </div>
      )}

      <section className={cn('skill-library-workspace', metaCollapsed && 'is-meta-collapsed')} aria-label={t('skills.libraryWorkspace')}>
        <aside className="skill-library-index-panel ui-surface" aria-label={t('skills.listHeading')}>
          <div className="skill-library-index-panel__header">
            <div className="skill-library-index-panel__title">
              <WrenchIcon aria-hidden="true" />
              <span>{t('skills.listHeading')}</span>
            </div>
            <span className="skill-library-index-panel__count">{loading ? '—' : items.length}</span>
          </div>
          <SkillSearchFilters
            compact
            domainOptions={domainOptions}
            filters={draftFilters}
            refreshing={refreshing}
            onChange={setDraftFilters}
            onClear={() => setDraftFilters(DEFAULT_FILTERS)}
            onRefresh={() => void refresh()}
          />
          <div className="skill-library-index-panel__list-heading">
            <span>{t('skills.package')}</span>
            <span>{t('skills.resultsCount', { count: items.length })}{hasMore ? ` · ${t('skills.loadMore')}` : ''}</span>
          </div>
          <SkillLibraryList
            items={items}
            loading={loading}
            loadingMore={loadingMore}
            hasMore={hasMore}
            error={error}
            highlightedId={highlightedId}
            selectedId={selectedId}
            filterActive={filterActive}
            sentinelRef={(node) => { sentinelRef.current = node }}
            onLoadMore={() => void loadMore()}
            onRetry={() => void (errorScope === 'initial' ? loadInitial(appliedFilters) : loadMore())}
            onClearFilter={() => setDraftFilters(DEFAULT_FILTERS)}
            onSelect={(item) => selectSkill(item.id)}
          />
        </aside>

        <section className="skill-library-reader-column" aria-live="polite">
          {loading || loadingDetail ? (
            <SkillLibraryDetailSkeleton />
          ) : detailError && !detail ? (
            <Alert variant="destructive" className="skill-library-detail-error" role="alert">
              <AlertCircleIcon className="size-4" />
              <AlertTitle>{t('skills.detailLoadFailed')}</AlertTitle>
              <AlertDescription>{detailError}</AlertDescription>
            </Alert>
          ) : !detail ? (
            <SkillLibraryNoSelection />
          ) : (
            <section className="skill-library-detail" aria-label={detail.name}>
              <header className="skill-library-detail__heading">
                <div className="min-w-0">
                  <p className="kicker">{t('skills.selectedSkill')} · {detail.domainName}</p>
                  <h2>{detail.name}</h2>
                  <p>{detail.description || t('skills.noDescription')}</p>
                </div>
                <div className="skill-library-detail__tools">
                  <Button type="button" size="sm" variant="outline" onClick={() => setMetaCollapsed((current) => !current)}>
                    {metaCollapsed
                      ? <PanelRightOpenIcon data-icon="inline-start" className="size-3.5" />
                      : <PanelRightCloseIcon data-icon="inline-start" className="size-3.5" />}
                    {metaCollapsed ? t('skills.showMetadata') : t('skills.collapseMetadata')}
                  </Button>
                  {metaCollapsed && (
                    <Button type="button" size="sm" variant="destructive" onClick={() => void deleteCurrentSkill()} disabled={deletingDetail}>
                      <Trash2Icon data-icon="inline-start" className="size-3.5" />
                      {t('skills.deleteSkill')}
                    </Button>
                  )}
                </div>
              </header>
              <div className="skill-library-detail__grid">
                <div className="skill-library-detail__main">
                  <SkillDetailReader file={mainFile} content={mainContent} loading={loadingMain} />
                  {fileError && (
                    <Alert variant="destructive" className="mt-4" role="alert">
                      <AlertCircleIcon className="size-4" />
                      <AlertTitle>{t('skills.fileLoadFailed')}</AlertTitle>
                      <AlertDescription>{fileError}</AlertDescription>
                    </Alert>
                  )}
                </div>
                {!metaCollapsed && (
                  <aside className="skill-library-meta" aria-label={t('skills.packageContext')}>
                    <SkillDetailMetadata detail={detail} sourceLabel={sourceLabel} />
                    <SkillDetailPackageFiles
                      count={detail.manifest.length}
                      nodes={fileTree}
                      selectedPath={selectedSupportPath || mainFile?.path || ''}
                      selectedFile={selectedSupportFile}
                      selectedContent={supportContent}
                      loadingSelectedFile={loadingSupport}
                      downloading={downloadingPath === selectedSupportFile?.path}
                      onSelect={(path) => setSelectedSupportPath(path === 'SKILL.md' ? '' : path)}
                      onDownload={() => void downloadFile(selectedSupportFile)}
                    />
                    <SkillDetailActions
                      mainFile={mainFile}
                      isZipPackage={detail.sourceType === 'zip'}
                      downloading={downloadingPath === PACKAGE_DOWNLOAD_KEY}
                      onDownload={() => void downloadMainPackage()}
                      deleting={deletingDetail}
                      onDelete={() => void deleteCurrentSkill()}
                    />
                  </aside>
                )}
              </div>
            </section>
          )}
        </section>
      </section>
    </PageFrame>
  )
}
