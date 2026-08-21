import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { CheckCircle2Icon, SearchIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Input } from '@/components/ui/input'
import { SkillActionBar } from './skill-action-bar'
import { SkillLibraryList } from './skill-library-list'
import { SkillPageHeader } from './skill-page-header'
import { listSkills, type SkillItem } from '@/lib/api'

const PAGE_SIZE = 20

interface SkillsNavigationState {
  highlightId?: string
  successSlug?: string
}

export function SkillsLibraryPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const navigationState = location.state as SkillsNavigationState | null
  const sentinelRef = useRef<HTMLDivElement | null>(null)
  const requestRef = useRef<AbortController | null>(null)
  const paginationRequestRef = useRef<AbortController | null>(null)
  const requestVersionRef = useRef(0)
  const [items, setItems] = useState<SkillItem[]>([])
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [hasMore, setHasMore] = useState(false)
  const [loading, setLoading] = useState(true)
  const [loadingMore, setLoadingMore] = useState(false)
  const [refreshing, setRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [errorScope, setErrorScope] = useState<'initial' | 'more' | null>(null)
  const [filter, setFilter] = useState('')
  const [highlightedId, setHighlightedId] = useState<string | null>(navigationState?.highlightId ?? null)
  const [notice, setNotice] = useState<string | null>(navigationState?.successSlug ? t('skills.uploadSuccess', { slug: navigationState.successSlug }) : null)

  const loadInitial = useCallback(async () => {
    const requestVersion = ++requestVersionRef.current
    requestRef.current?.abort()
    paginationRequestRef.current?.abort()
    const controller = new AbortController()
    requestRef.current = controller
    setLoading(true)
    setLoadingMore(false)
    setError(null)
    setErrorScope(null)
    try {
      const page = await listSkills(PAGE_SIZE, null, controller.signal)
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
      const page = await listSkills(PAGE_SIZE, nextCursor, controller.signal)
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
  }, [hasMore, loadingMore, nextCursor, t])

  useEffect(() => {
    void loadInitial()
    return () => {
      requestRef.current?.abort()
      paginationRequestRef.current?.abort()
    }
  }, [loadInitial])

  useEffect(() => {
    const node = sentinelRef.current
    if (!node || loading || !hasMore) return
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting) void loadMore()
      },
      { rootMargin: '360px 0px' },
    )
    observer.observe(node)
    return () => observer.disconnect()
  }, [hasMore, loadMore, loading])

  useEffect(() => {
    if (!navigationState?.highlightId) return
    navigate(location.pathname, { replace: true, state: null })
  }, [location.pathname, navigate, navigationState?.highlightId])

  useEffect(() => {
    if (!highlightedId) return
    const timeout = window.setTimeout(() => setHighlightedId(null), 5000)
    return () => window.clearTimeout(timeout)
  }, [highlightedId])

  const visibleItems = useMemo(() => {
    const query = filter.trim().toLowerCase()
    if (!query) return items
    return items.filter((item) => `${item.name} ${item.slug} ${item.domainName} ${item.description}`.toLowerCase().includes(query))
  }, [filter, items])

  const refresh = async () => {
    setRefreshing(true)
    try {
      await loadInitial()
    } finally {
      setRefreshing(false)
    }
  }

  return (
    <div className="mx-auto max-w-6xl">
      <SkillPageHeader
        eyebrow={t('skills.libraryKicker')}
        title={t('skills.libraryTitle')}
        description={t('skills.libraryDescription')}
        actions={<Badge variant="accent" className="font-mono text-[10px]">{t('skills.localOnly')}</Badge>}
      />

      {notice && (
        <div className="mb-5 flex items-start gap-3 rounded-xl border border-ok/30 bg-ok/10 px-4 py-3 text-sm" role="status">
          <CheckCircle2Icon className="mt-0.5 size-4 shrink-0 text-ok" />
          <div className="min-w-0 flex-1">{notice}</div>
          {highlightedId && <Link className="shrink-0 text-xs font-medium text-primary underline-offset-4 hover:underline" to={`/skills/editor/${highlightedId}`}>{t('skills.continueEditing')}</Link>}
          <button type="button" className="text-muted-foreground hover:text-foreground" aria-label={t('skills.dismissNotice')} onClick={() => setNotice(null)}>×</button>
        </div>
      )}

      <SkillActionBar
        count={items.length}
        refreshing={refreshing}
        onCreate={() => navigate('/skills/editor')}
        onUpload={() => navigate('/skills/upload')}
        onRefresh={() => void refresh()}
      />

      <div className="mb-5 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="kicker">{t('skills.installedKicker')}</p>
          <p className="mt-1 text-xs text-muted-foreground">{t('skills.installedDescription')}</p>
        </div>
        <label className="relative block w-full sm:w-72">
          <span className="sr-only">{t('skills.filterInstalled')}</span>
          <SearchIcon className="pointer-events-none absolute left-3 top-1/2 size-3.5 -translate-y-1/2 text-muted-foreground" />
          <Input value={filter} onChange={(event) => setFilter(event.target.value)} placeholder={t('skills.filterInstalled')} className="h-9 pl-9 text-xs" />
        </label>
      </div>

      <SkillLibraryList
        items={visibleItems}
        loading={loading}
        loadingMore={loadingMore}
        hasMore={hasMore}
        error={error}
        highlightedId={highlightedId}
        filterActive={Boolean(filter.trim())}
        sentinelRef={(node) => { sentinelRef.current = node }}
        onLoadMore={() => void loadMore()}
        onRetry={() => void (errorScope === 'initial' ? loadInitial() : loadMore())}
        onUpload={() => navigate('/skills/upload')}
        onCreate={() => navigate('/skills/editor')}
        onClearFilter={() => setFilter('')}
      />
    </div>
  )
}
