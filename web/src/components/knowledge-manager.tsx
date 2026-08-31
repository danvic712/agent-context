import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  ArrowLeftIcon,
  BookOpenIcon,
  CheckIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  CircleHelpIcon,
  EyeOffIcon,
  LoaderCircleIcon,
  RotateCcwIcon,
  SearchXIcon,
  SearchIcon,
  TrashIcon,
  XIcon,
} from 'lucide-react'
import { ActionBar, ActionBarStatus } from '@/components/ui/action-bar'
import { useActionFeedback } from '@/components/ui/action-feedback'
import { PageFrame } from '@/components/ui/page-frame'
import { Surface } from '@/components/ui/surface'
import { KnowledgeDetailSkeleton, KnowledgeListSkeleton } from '@/components/ui/loading-skeletons'
import { Skeleton } from '@/components/ui/skeleton'
import {
  deleteKnowledge,
  getUserFacingError,
  listKnowledgeLibrary,
  rateKnowledge,
  restoreKnowledge,
  sendKnowledgeToReview,
  setKnowledgePrivate,
  type KnowledgeItem,
  type KnowledgeLibraryPage,
  type KnowledgeStatus,
} from '@/lib/api'
import { formatDate, formatDateTime } from '@/lib/formatting'
import './knowledge-manager.css'

const PAGE_SIZE = 30
const STATUSES: KnowledgeStatus[] = ['Active', 'Review', 'Archived']

function isKnowledgeLibraryPage(value: unknown): value is KnowledgeLibraryPage {
  if (!value || typeof value !== 'object') return false
  const page = value as Partial<KnowledgeLibraryPage>
  return Array.isArray(page.items)
    && typeof page.hasMore === 'boolean'
    && typeof page.counts?.active === 'number'
    && typeof page.counts.review === 'number'
    && typeof page.counts.archived === 'number'
}

interface KnowledgeManagerProps {
  // Kept intentionally empty: Active, Review and Archived are one library surface.
}

export function KnowledgeManager(_props: KnowledgeManagerProps) {
  const { t } = useTranslation()
  const { push } = useActionFeedback()
  const sentinelRef = useRef<HTMLDivElement | null>(null)
  const initialRequestRef = useRef<AbortController | null>(null)
  const paginationRequestRef = useRef<AbortController | null>(null)
  const requestVersionRef = useRef(0)
  const [status, setStatus] = useState<KnowledgeStatus>('Active')
  const [search, setSearch] = useState('')
  const [appliedSearch, setAppliedSearch] = useState('')
  const [items, setItems] = useState<KnowledgeItem[]>([])
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [hasMore, setHasMore] = useState(false)
  const [counts, setCounts] = useState({ active: 0, review: 0, archived: 0 })
  const [reviewThreshold, setReviewThreshold] = useState<number | null>(null)
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [mutatingId, setMutatingId] = useState<string | null>(null)
  const [mobileView, setMobileView] = useState<'list' | 'detail'>('list')
  const listScrollTopRef = useRef(0)

  const statusMeta: Record<KnowledgeStatus, { label: string; tone: 'blue' | 'amber' | 'red' }> = {
    Active: { label: t('knowledge.active'), tone: 'blue' },
    Review: { label: t('knowledge.review'), tone: 'amber' },
    Archived: { label: t('knowledge.archived'), tone: 'red' },
  }

  useEffect(() => {
    const timeout = window.setTimeout(() => setAppliedSearch(search.trim()), 250)
    return () => window.clearTimeout(timeout)
  }, [search])

  const loadInitial = useCallback(async (nextStatus: KnowledgeStatus, query: string) => {
    const requestVersion = ++requestVersionRef.current
    initialRequestRef.current?.abort()
    paginationRequestRef.current?.abort()
    const controller = new AbortController()
    initialRequestRef.current = controller
    setLoading(true)
    setLoadingMore(false)
    setError(null)

    try {
      const page = await listKnowledgeLibrary(nextStatus, PAGE_SIZE, null, query, controller.signal)
      if (controller.signal.aborted || requestVersion !== requestVersionRef.current) return
      if (!isKnowledgeLibraryPage(page)) throw new Error(t('knowledge.failedLoad'))
      setItems(page.items)
      setNextCursor(page.nextCursor)
      setHasMore(page.hasMore)
      setCounts(page.counts)
      setReviewThreshold(page.reviewThreshold)
      setSelectedId((current) => page.items.some((item) => item.id === current) ? current : page.items[0]?.id ?? null)
    } catch (cause) {
      if (!controller.signal.aborted && requestVersion === requestVersionRef.current) {
        setError(getUserFacingError(cause, t('knowledge.failedLoad')))
      }
    } finally {
      if (!controller.signal.aborted && requestVersion === requestVersionRef.current) setLoading(false)
    }
  }, [t])

  const loadMore = useCallback(async () => {
    if (!hasMore || loading || loadingMore || !nextCursor) return
    const requestVersion = requestVersionRef.current
    const controller = new AbortController()
    paginationRequestRef.current?.abort()
    paginationRequestRef.current = controller
    setLoadingMore(true)
    try {
      const page = await listKnowledgeLibrary(status, PAGE_SIZE, nextCursor, appliedSearch, controller.signal)
      if (controller.signal.aborted || requestVersion !== requestVersionRef.current) return
      if (!isKnowledgeLibraryPage(page)) throw new Error(t('knowledge.failedLoadMore'))
      setItems((current) => {
        const known = new Set(current.map((item) => item.id))
        return [...current, ...page.items.filter((item) => !known.has(item.id))]
      })
      setNextCursor(page.nextCursor)
      setHasMore(page.hasMore)
      setCounts(page.counts)
    } catch (cause) {
      if (!controller.signal.aborted && requestVersion === requestVersionRef.current) {
        setError(getUserFacingError(cause, t('knowledge.failedLoadMore')))
      }
    } finally {
      if (requestVersion === requestVersionRef.current) setLoadingMore(false)
    }
  }, [appliedSearch, hasMore, loading, loadingMore, nextCursor, status, t])

  useEffect(() => {
    void loadInitial(status, appliedSearch)
    return () => {
      initialRequestRef.current?.abort()
      paginationRequestRef.current?.abort()
    }
  }, [appliedSearch, loadInitial, status])

  useEffect(() => {
    const node = sentinelRef.current
    if (!node || loading || !hasMore) return
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) void loadMore()
      },
      { rootMargin: '320px 0px' },
    )
    observer.observe(node)
    return () => observer.disconnect()
  }, [hasMore, loadMore, loading])

  const refresh = async () => {
    await loadInitial(status, appliedSearch)
  }

  const mutate = async (item: KnowledgeItem, action: () => Promise<void>) => {
    setMutatingId(item.id)
    setError(null)
    let succeeded = false
    try {
      await action()
      await refresh()
      succeeded = true
    } catch (cause) {
      setError(getUserFacingError(cause, t('knowledge.failedUpdate')))
    } finally {
      setMutatingId(null)
    }
    return succeeded
  }

  const remove = async (item: KnowledgeItem) => {
    if (!window.confirm(t('knowledge.deleteConfirm', { title: item.title }))) return
    if (await mutate(item, () => deleteKnowledge(item.id))) push(t('knowledge.deleteSuccess'), 'success')
  }

  const selectedItem = useMemo(
    () => items.find((item) => item.id === selectedId) ?? items[0] ?? null,
    [items, selectedId],
  )

  useEffect(() => {
    if (!selectedItem && mobileView === 'detail') setMobileView('list')
  }, [mobileView, selectedItem])

  const countFor = (value: KnowledgeStatus) => {
    if (value === 'Active') return counts.active
    if (value === 'Review') return counts.review
    return counts.archived
  }

  const selectedIndex = selectedItem ? items.findIndex((item) => item.id === selectedItem.id) : -1
  const typeLabel = (type: KnowledgeItem['type']) => t(`knowledge.type${type}` as 'knowledge.typeProblem' | 'knowledge.typeSolution' | 'knowledge.typePattern')
  const confidencePercent = selectedItem ? Math.round(Math.max(0, Math.min(1, selectedItem.confidence)) * 100) : 0
  const selectedConfidenceIsLow = selectedItem ? selectedItem.confidence < (reviewThreshold ?? 0.5) : false
  const listSummary = hasMore
    ? t('knowledge.showingItems', { count: items.length })
    : t('knowledge.itemsCount', { count: items.length })
  const initialLoading = loading && items.length === 0
  const navigateSelected = (direction: -1 | 1) => {
    if (items.length === 0 || selectedIndex < 0) return
    const nextIndex = (selectedIndex + direction + items.length) % items.length
    setSelectedId(items[nextIndex].id)
  }
  const openItem = (id: string) => {
    setSelectedId(id)
    if (!window.matchMedia('(max-width: 900px)').matches) return
    listScrollTopRef.current = window.scrollY
    setMobileView('detail')
    window.requestAnimationFrame(() => window.scrollTo({ top: 0, behavior: 'auto' }))
  }
  const backToList = () => {
    setMobileView('list')
    window.requestAnimationFrame(() => window.scrollTo({ top: listScrollTopRef.current, behavior: 'auto' }))
  }
  const typeTone = (type: KnowledgeItem['type']) => type === 'Solution' ? 'blue' : type === 'Pattern' ? 'green' : 'amber'
  const detailReason = selectedItem ? t(`knowledge.detailReason${selectedItem.status}`) : ''
  const statusNote = status === 'Review' && reviewThreshold !== null
    ? t('knowledge.reviewThresholdNote', { threshold: Math.round(reviewThreshold * 100) })
    : status === 'Archived'
      ? t('knowledge.archivedNote')
      : t('knowledge.activeNote')
  const runAction = async (item: KnowledgeItem, action: () => Promise<void>, successMessage: string) => {
    if (await mutate(item, action)) push(successMessage, 'success')
  }

  return (
    <PageFrame className="knowledge-page">
      <Surface as="section" className="knowledge-library-snapshot" aria-label={t('knowledge.workspaceTitle')}>
        <div className="knowledge-library-snapshot__intro">
          <p>{t('knowledge.workspaceKicker')}</p>
          <h2>{t('knowledge.workspaceTitle')}</h2>
          <span>{t('knowledge.workspaceDescription')}</span>
        </div>
        {STATUSES.map((value) => (
          <div key={value} className={`knowledge-library-snapshot__stat knowledge-library-snapshot__stat--${statusMeta[value].tone}`}>
            <strong>{initialLoading ? <Skeleton className="h-6 w-8" /> : countFor(value)}</strong>
            <span>{statusMeta[value].label}</span>
            <small>{t(`knowledge.${value === 'Active' ? 'activeSummary' : value === 'Review' ? 'reviewSummary' : 'archivedSummary'}`)}</small>
          </div>
        ))}
      </Surface>

      <Surface as="section" className="knowledge-library-workspace" data-mobile-view={mobileView} aria-busy={loading}>
        <aside className="knowledge-index-panel" aria-label={t('knowledge.itemsTitle')}>
          <div className="knowledge-pane-head">
            <div className="knowledge-pane-title-row">
              <div className="knowledge-pane-title">{t('knowledge.itemsTitle')}</div>
              {!loading && <span className="knowledge-result-count">{listSummary}</span>}
            </div>
            <div className="knowledge-search">
              <SearchIcon aria-hidden="true" />
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder={t('knowledge.searchPlaceholder')}
                aria-label={t('knowledge.searchPlaceholder')}
                type="search"
              />
              {search && (
                <button
                  type="button"
                  className="knowledge-search__clear"
                  onClick={() => setSearch('')}
                  aria-label={t('knowledge.clearSearch')}
                >
                  <XIcon aria-hidden="true" />
                </button>
              )}
            </div>
          </div>

          <div className="knowledge-status-tabs" role="tablist" aria-label={t('knowledge.libraryTitle')}>
            {STATUSES.map((value) => (
              <button
                key={value}
                type="button"
                id={`knowledge-tab-${value.toLowerCase()}`}
                role="tab"
                aria-selected={status === value}
                aria-controls="knowledge-status-panel"
                onClick={() => setStatus(value)}
                className="knowledge-status-tab"
              >
                <span className="knowledge-status-tab__label">{statusMeta[value].label}</span>
                <span className="knowledge-count">
                  {loading && items.length === 0 ? <Skeleton className="h-2.5 w-3" /> : countFor(value)}
                </span>
              </button>
            ))}
          </div>

          <details className="knowledge-index-advanced">
            <summary>
              <span>{t('knowledge.reviewGuide')}</span>
              <span>{status === 'Review' ? t('knowledge.lowConfidence') : statusMeta[status].label}</span>
            </summary>
            <div className="knowledge-status-note" data-status={status}>
              <CircleHelpIcon aria-hidden="true" />
              <span>{statusNote}</span>
            </div>
          </details>

          {loading && items.length > 0 && (
            <div className="knowledge-refreshing" role="status">
              <LoaderCircleIcon aria-hidden="true" />
              <span>{t('common.loading')}</span>
            </div>
          )}

          <div className="knowledge-index-list-heading">
            <span>{statusMeta[status].label}</span>
            <span>{listSummary}</span>
          </div>

          <div id="knowledge-status-panel" className="knowledge-index-list" role="tabpanel" aria-labelledby={`knowledge-tab-${status.toLowerCase()}`} aria-busy={loading}>
            {error && (
              <div className="knowledge-error" role="alert">
                <span>{error}</span>
                <button type="button" onClick={() => void refresh()}>{t('knowledge.retry')}</button>
              </div>
            )}
            {initialLoading ? (
              <KnowledgeListSkeleton label={t('common.loading')} />
            ) : items.length === 0 && !error ? (
              <div className="knowledge-empty">
                <SearchXIcon aria-hidden="true" />
                <strong>{search.trim() ? t('knowledge.noMatches') : t(`knowledge.empty${status}` as 'knowledge.emptyActive' | 'knowledge.emptyReview' | 'knowledge.emptyArchived')}</strong>
                <span>{search.trim() ? t('knowledge.noMatchesDescription') : t('knowledge.emptyDescription')}</span>
              </div>
            ) : (
              items.map((item) => (
                <div key={item.id} className={`knowledge-index-row${item.id === selectedItem?.id ? ' is-selected' : ''}`} role="listitem">
                  <button
                    type="button"
                    data-selected={item.id === selectedItem?.id}
                    onClick={() => openItem(item.id)}
                    className="knowledge-index-select"
                    aria-current={item.id === selectedItem?.id ? 'true' : undefined}
                    aria-label={t('knowledge.itemAria', { title: item.title, type: typeLabel(item.type) })}
                  >
                    <span className={`knowledge-index-avatar knowledge-index-avatar--${typeTone(item.type)}`} aria-hidden="true"><BookOpenIcon /></span>
                    <span className="knowledge-index-copy">
                      <strong>{item.title}</strong>
                      <small>{item.domainName ?? t('knowledge.noDomain')} · {typeLabel(item.type)}</small>
                      <em>{item.content}</em>
                    </span>
                    <span className="knowledge-index-row-end">
                      <time dateTime={item.updatedAtUtc} title={formatDateTime(item.updatedAtUtc)}>{formatDate(item.updatedAtUtc)}</time>
                      <ChevronRightIcon aria-hidden="true" />
                    </span>
                  </button>
                </div>
              ))
            )}
            <div ref={sentinelRef} aria-hidden="true" className="h-1" />
            {hasMore && (
              <button type="button" className="knowledge-load-more" onClick={() => void loadMore()} disabled={loading || loadingMore}>
                {loadingMore && <LoaderCircleIcon className="mr-1.5 inline-block size-3.5 animate-spin align-[-2px]" />}
                {loadingMore ? t('knowledge.loadingMore') : t('knowledge.loadMore')}
              </button>
            )}
          </div>
        </aside>

        <section className="knowledge-reader-column" aria-labelledby={selectedItem ? 'knowledge-detail-title' : undefined}>
          {selectedItem ? (
            <article className="knowledge-reader" aria-label={selectedItem.title}>
              <button type="button" className="knowledge-mobile-back" onClick={backToList}>
                <ArrowLeftIcon aria-hidden="true" />
                {t('knowledge.backToList')}
              </button>
              <header className="knowledge-reader-heading">
                <div>
                  <div className="knowledge-reader-kicker">
                    <span>{t('knowledge.selectedItem')}</span>
                    <span aria-hidden="true">·</span>
                    <span>{selectedItem.domainName ?? t('knowledge.noDomain')}</span>
                  </div>
                  <h2 id="knowledge-detail-title" className="knowledge-reader-title">{selectedItem.title}</h2>
                  <p className="knowledge-reader-description">{detailReason}</p>
                  <div className="knowledge-reader-status">
                    <span className={`knowledge-tag knowledge-tag--${statusMeta[selectedItem.status].tone}`}>{statusMeta[selectedItem.status].label}</span>
                    <span className={`knowledge-tag knowledge-tag--${typeTone(selectedItem.type)}`}>{typeLabel(selectedItem.type)}</span>
                    {selectedItem.isPrivate && <span className="knowledge-tag knowledge-tag--private"><EyeOffIcon aria-hidden="true" />{t('knowledge.private')}</span>}
                  </div>
                </div>
                <div className="knowledge-reader-tools">
                  <span className="knowledge-reader-position">{t('knowledge.itemPosition', { current: selectedIndex + 1, total: items.length })}</span>
                  <button type="button" className="knowledge-reader-icon-button" onClick={() => navigateSelected(-1)} aria-label={t('knowledge.previousItem')} disabled={items.length < 2}>
                    <ChevronLeftIcon aria-hidden="true" />
                  </button>
                  <button type="button" className="knowledge-reader-icon-button" onClick={() => navigateSelected(1)} aria-label={t('knowledge.nextItem')} disabled={items.length < 2}>
                    <ChevronRightIcon aria-hidden="true" />
                  </button>
                </div>
              </header>

              <div className="knowledge-reader-grid">
                <main className="knowledge-reader-main">
                  <div className="knowledge-reader-header">
                    <span>{t('knowledge.noteLabel')} · {selectedItem.id.slice(0, 8).toUpperCase()}</span>
                    <span className={`knowledge-tag knowledge-tag--${typeTone(selectedItem.type)}`}>{typeLabel(selectedItem.type)}</span>
                  </div>
                  <div className="knowledge-detail-body">
                <div className="knowledge-content-label">{t('knowledge.summaryLabel')}</div>
                <p>{selectedItem.content}</p>
                <div className="knowledge-callout">
                  <CircleHelpIcon aria-hidden="true" />
                  <span><strong>{t('knowledge.whyHere')}</strong> {detailReason}</span>
                </div>

                <ActionBar
                  sticky
                  className="knowledge-detail-footer"
                  status={<ActionBarStatus>{t('knowledge.operationLabel')}</ActionBarStatus>}
                >
                  {selectedItem.status === 'Archived' ? (
                    <button type="button" className="knowledge-action knowledge-action--primary" onClick={() => void runAction(selectedItem, () => restoreKnowledge(selectedItem.id), t('knowledge.restoreSuccess'))} disabled={mutatingId === selectedItem.id}>
                      <RotateCcwIcon />{t('knowledge.restoreToActive')}
                    </button>
                  ) : selectedItem.status === 'Review' ? (
                    <>
                      <button type="button" className="knowledge-action knowledge-action--primary" onClick={() => void runAction(selectedItem, async () => { await rateKnowledge(selectedItem.id, true) }, t('knowledge.usefulSuccess'))} disabled={mutatingId === selectedItem.id}>
                        <CheckIcon />{t('knowledge.confirmUseful')}
                      </button>
                      <button type="button" className="knowledge-action" onClick={() => void refresh().then(() => push(t('knowledge.deferSuccess'), 'info'))} disabled={mutatingId === selectedItem.id}>
                        {t('knowledge.deferReview')}
                      </button>
                    </>
                  ) : (
                    <>
                      <button type="button" className="knowledge-action" onClick={() => void runAction(selectedItem, () => setKnowledgePrivate(selectedItem.id, !selectedItem.isPrivate), selectedItem.isPrivate ? t('knowledge.unmarkPrivateSuccess') : t('knowledge.markPrivateSuccess'))} disabled={mutatingId === selectedItem.id}>
                        {selectedItem.isPrivate ? t('knowledge.unmarkPrivate') : t('knowledge.markPrivate')}
                      </button>
                      <button type="button" className="knowledge-action knowledge-action--warning" onClick={() => void runAction(selectedItem, () => sendKnowledgeToReview(selectedItem.id), t('knowledge.reviewSuccess'))} disabled={mutatingId === selectedItem.id}>
                        {t('knowledge.sendToReview')}
                      </button>
                    </>
                  )}
                  <button type="button" className="knowledge-action knowledge-action--danger" onClick={() => void remove(selectedItem)} disabled={mutatingId === selectedItem.id}>
                    <TrashIcon />{t('common.delete')}
                  </button>
                </ActionBar>
                  </div>
                </main>

                <aside className="knowledge-context" aria-label={t('knowledge.contextTitle')}>
                  <div className="knowledge-context__heading">
                    <strong>{t('knowledge.contextTitle')}</strong>
                    <span>{selectedItem.isPrivate ? t('knowledge.private') : t('knowledge.shared')}</span>
                  </div>
                  <div className="knowledge-context__confidence" aria-label={t('knowledge.confidenceValue', { value: confidencePercent })}>
                    <div className="knowledge-context__confidence-top">
                      <span>{t('knowledge.confidenceLabel')}</span>
                      <strong>{confidencePercent}%</strong>
                    </div>
                    <div className="knowledge-confidence-meter" aria-hidden="true">
                      <span style={{ width: `${confidencePercent}%` }} data-low={selectedConfidenceIsLow} />
                    </div>
                    <p>{t('knowledge.confidenceMeaning')}</p>
                  </div>
                  <div className="knowledge-context__row">
                    <span>{t('knowledge.sourceSession')}</span>
                    <strong>{selectedItem.sourceSessionTask ?? t('knowledge.noSource')}</strong>
                  </div>
                  <div className="knowledge-context__row">
                    <span>{t('knowledge.updated')}</span>
                    <strong>{formatDateTime(selectedItem.updatedAtUtc)}</strong>
                  </div>
                  <div className="knowledge-context__row">
                    <span>{t('knowledge.lastUsed')}</span>
                    <strong>{selectedItem.lastUsedAtUtc ? formatDateTime(selectedItem.lastUsedAtUtc) : t('knowledge.neverUsed')}</strong>
                  </div>
                  <div className="knowledge-context__note">{detailReason}</div>
                </aside>
              </div>
            </article>
          ) : loading && items.length === 0 ? (
            <KnowledgeDetailSkeleton label={t('common.loading')} />
          ) : (
            <div className="knowledge-reader-empty">
              {t('knowledge.selectItem')}
            </div>
          )}
        </section>
      </Surface>
    </PageFrame>
  )
}
