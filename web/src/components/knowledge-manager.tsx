import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  ArrowLeftIcon,
  CheckIcon,
  LoaderCircleIcon,
  RotateCcwIcon,
  SearchIcon,
  TrashIcon,
} from 'lucide-react'
import { ActionBar } from '@/components/ui/action-bar'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
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
    try {
      await action()
      await refresh()
    } catch (cause) {
      setError(getUserFacingError(cause, t('knowledge.failedUpdate')))
    } finally {
      setMutatingId(null)
    }
  }

  const remove = async (item: KnowledgeItem) => {
    if (!window.confirm(t('knowledge.deleteConfirm', { title: item.title }))) return
    await mutate(item, () => deleteKnowledge(item.id))
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

  return (
    <PageFrame
      className="knowledge-page"
      header={(
        <PageHeader
          eyebrow={t('knowledge.libraryKicker')}
          title={t('knowledge.libraryTitle')}
          description={t('knowledge.libraryDescription')}
        />
      )}
    >
      <Surface as="div" className="knowledge-library-split" data-mobile-view={mobileView}>
        <aside className="knowledge-library-left">
          <div className="knowledge-pane-head">
            <div className="knowledge-pane-title">{t('knowledge.itemsTitle')}</div>
            <label className="knowledge-search">
              <SearchIcon aria-hidden="true" />
              <input
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder={t('knowledge.searchPlaceholder')}
                aria-label={t('knowledge.searchPlaceholder')}
                type="search"
              />
            </label>
          </div>

          <div className="knowledge-status-tabs" role="group" aria-label={t('knowledge.libraryTitle')}>
            {STATUSES.map((value) => (
              <button
                key={value}
                type="button"
                aria-pressed={status === value}
                onClick={() => setStatus(value)}
                className="knowledge-status-tab"
              >
                {statusMeta[value].label}
                <span className="knowledge-count">
                  {loading && items.length === 0 ? <Skeleton className="h-2.5 w-3" /> : countFor(value)}
                </span>
              </button>
            ))}
          </div>

          <div className="knowledge-confidence-note">
            <span className="knowledge-confidence-info" aria-hidden="true">i</span>
            <span><strong>{t('knowledge.confidenceLabel')}</strong> {t('knowledge.confidenceMeaning')}</span>
          </div>

          <div className="knowledge-list" aria-busy={loading}>
            {error && <p className="knowledge-error" role="alert">{error}</p>}
            {loading && items.length > 0 && (
              <div className="knowledge-refreshing" role="status">
                <LoaderCircleIcon aria-hidden="true" />
                <span>{t('common.loading')}</span>
              </div>
            )}
            {loading && items.length === 0 ? (
              <KnowledgeListSkeleton label={t('common.loading')} />
            ) : items.length === 0 ? (
              <div className="knowledge-empty">
                {search.trim() ? t('knowledge.noMatches') : t(`knowledge.empty${status}` as 'knowledge.emptyActive' | 'knowledge.emptyReview' | 'knowledge.emptyArchived')}
              </div>
            ) : (
              items.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  data-selected={item.id === selectedItem?.id}
                  onClick={() => openItem(item.id)}
                  className="knowledge-item"
                >
                  <span className="knowledge-item-top">
                    <span className="knowledge-item-title-wrap">
                      <span className="knowledge-item-title">{item.title}</span>
                      <span className="knowledge-item-kind">
                        <span className={`knowledge-tag knowledge-tag--${typeTone(item.type)}`}>{item.type}</span>
                      </span>
                    </span>
                    <span className={`knowledge-item-confidence ${item.confidence < (reviewThreshold ?? 0.5) ? 'knowledge-item-confidence--low' : ''}`}>
                      {t('knowledge.confidenceValue', { value: Math.round(item.confidence * 100) })}
                    </span>
                  </span>
                  <span className="knowledge-item-content">{item.content}</span>
                  <span className="knowledge-item-foot">
                    <span className="knowledge-item-domain">{item.domainName ?? t('knowledge.noDomain')}</span>
                    <span aria-hidden="true">·</span>
                    <span>{formatDate(item.updatedAtUtc)}</span>
                  </span>
                </button>
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

        <section className="knowledge-library-right">
          {selectedItem ? (
            <div className="knowledge-detail">
              <button type="button" className="knowledge-mobile-back" onClick={backToList}>
                <ArrowLeftIcon aria-hidden="true" />
                {t('knowledge.backToList')}
              </button>
              <div className="knowledge-detail-top">
                <div>
                  <div className="knowledge-detail-kicker">{t('knowledge.detailKicker')}　·　{selectedItem.domainName ?? t('knowledge.noDomain')}</div>
                  <h2 className="knowledge-detail-title">{selectedItem.title}</h2>
                  <div className="knowledge-detail-status">
                    <span className={`knowledge-tag knowledge-tag--${statusMeta[selectedItem.status].tone}`}>{statusMeta[selectedItem.status].label}</span>
                  </div>
                </div>
                <div className="knowledge-detail-tools">
                  <span className="knowledge-detail-position">{selectedIndex + 1} / {items.length}</span>
                  <button type="button" className="knowledge-icon-button" onClick={() => navigateSelected(-1)} aria-label={t('knowledge.previousItem')}>←</button>
                  <button type="button" className="knowledge-icon-button" onClick={() => navigateSelected(1)} aria-label={t('knowledge.nextItem')}>→</button>
                </div>
              </div>

              <div className="knowledge-detail-body">
                <p>{selectedItem.content}</p>
                <div className="knowledge-callout">
                  <strong>{t('knowledge.whyHere')}</strong> {detailReason}
                </div>
                <div className="knowledge-detail-section">
                  <div className="knowledge-section-label">{t('knowledge.sourceAndUpdated')}</div>
                  <div className="knowledge-meta-grid">
                    <div className="knowledge-meta-cell">
                      <label>{t('knowledge.sourceSession')}</label>
                      <strong>{selectedItem.sourceSessionTask ?? t('knowledge.noSource')}</strong>
                    </div>
                    <div className="knowledge-meta-cell">
                      <label>{t('knowledge.updated')}</label>
                      <strong>{formatDateTime(selectedItem.updatedAtUtc)}</strong>
                    </div>
                  </div>
                </div>

                <ActionBar
                  sticky
                  className="knowledge-detail-footer"
                  status={<span className="knowledge-detail-footer-label">{t('knowledge.operationLabel')}</span>}
                >
                  {selectedItem.status === 'Archived' ? (
                    <button type="button" className="knowledge-action knowledge-action--primary" onClick={() => void mutate(selectedItem, () => restoreKnowledge(selectedItem.id))} disabled={mutatingId === selectedItem.id}>
                      <RotateCcwIcon />{t('knowledge.restoreToActive')}
                    </button>
                  ) : selectedItem.status === 'Review' ? (
                    <>
                      <button type="button" className="knowledge-action knowledge-action--primary" onClick={() => void mutate(selectedItem, async () => { await rateKnowledge(selectedItem.id, true) })} disabled={mutatingId === selectedItem.id}>
                        <CheckIcon />{t('knowledge.confirmUseful')}
                      </button>
                      <button type="button" className="knowledge-action" onClick={() => void refresh()} disabled={mutatingId === selectedItem.id}>
                        {t('knowledge.deferReview')}
                      </button>
                    </>
                  ) : (
                    <>
                      <button type="button" className="knowledge-action" onClick={() => void mutate(selectedItem, () => setKnowledgePrivate(selectedItem.id, !selectedItem.isPrivate))} disabled={mutatingId === selectedItem.id}>
                        {selectedItem.isPrivate ? t('knowledge.unmarkPrivate') : t('knowledge.markPrivate')}
                      </button>
                      <button type="button" className="knowledge-action knowledge-action--warning" onClick={() => void mutate(selectedItem, () => sendKnowledgeToReview(selectedItem.id))} disabled={mutatingId === selectedItem.id}>
                        {t('knowledge.sendToReview')}
                      </button>
                    </>
                  )}
                  <button type="button" className="knowledge-action knowledge-action--danger" onClick={() => void remove(selectedItem)} disabled={mutatingId === selectedItem.id}>
                    <TrashIcon />{t('common.delete')}
                  </button>
                </ActionBar>
              </div>
            </div>
          ) : loading && items.length === 0 ? (
            <KnowledgeDetailSkeleton label={t('common.loading')} />
          ) : (
            <div className="knowledge-detail flex items-center justify-center text-sm text-muted-foreground">
              {t('knowledge.selectItem')}
            </div>
          )}
        </section>
      </Surface>
    </PageFrame>
  )
}
