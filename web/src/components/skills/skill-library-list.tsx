import { BookOpenIcon, ChevronRightIcon, LoaderCircleIcon, RefreshCwIcon, SearchXIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import type { SkillItem } from '@/lib/api'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import { skillSourceKey } from './skill-source'

interface SkillLibraryListProps {
  items: SkillItem[]
  loading: boolean
  loadingMore: boolean
  hasMore: boolean
  error: string | null
  highlightedId: string | null
  selectedId: string | null
  filterActive?: boolean
  sentinelRef?: (node: HTMLDivElement | null) => void
  onLoadMore: () => void
  onRetry: () => void
  onClearFilter?: () => void
  onSelect: (item: SkillItem) => void
}

export function SkillLibraryList({
  items,
  loading,
  loadingMore,
  hasMore,
  error,
  highlightedId,
  selectedId,
  filterActive = false,
  sentinelRef,
  onLoadMore,
  onRetry,
  onClearFilter,
  onSelect,
}: SkillLibraryListProps) {
  const { t } = useTranslation()

  if (loading) {
    return (
      <div className="skill-library-index__items" aria-busy="true" aria-label={t('skills.loading')}>
        {Array.from({ length: 8 }, (_, index) => (
          <div key={index} className="skill-library-index__skeleton">
            <Skeleton className="size-8 rounded-xl" />
            <div className="min-w-0 flex-1 space-y-2">
              <Skeleton className="h-3.5 w-3/4" />
              <Skeleton className="h-2.5 w-1/2" />
            </div>
            <Skeleton className="h-3 w-7" />
          </div>
        ))}
      </div>
    )
  }

  if (error && items.length === 0) {
    return (
      <Alert variant="destructive">
        <RefreshCwIcon />
        <AlertTitle>{t('skills.failedLoad')}</AlertTitle>
        <AlertDescription className="flex flex-wrap items-center gap-3">
          <span>{error}</span>
          <Button type="button" size="sm" variant="outline" onClick={onRetry}>
            {t('common.retry')}
          </Button>
        </AlertDescription>
      </Alert>
    )
  }

  if (items.length === 0 && filterActive) {
    return (
      <>
        <Card className="skill-library-empty border-dashed bg-card/70 p-0">
          <CardContent className="flex flex-col items-center justify-center px-6 py-14 text-center">
            <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
              <SearchXIcon className="size-6" />
            </div>
            <h2 className="serif mt-4 text-2xl font-semibold">{t('skills.noMatchesTitle')}</h2>
            <p className="mt-2 max-w-md text-sm leading-6 text-muted-foreground">{t('skills.noMatchesDescription')}</p>
            {onClearFilter && <Button type="button" variant="outline" className="mt-5" onClick={onClearFilter}>{t('skills.clearFilter')}</Button>}
          </CardContent>
        </Card>
        <div ref={sentinelRef} aria-hidden="true" className="h-2" />
        <div className="skill-library-pagination flex flex-col items-center justify-center gap-3 text-center">
          {hasMore ? (
            <Button type="button" variant="outline" onClick={onLoadMore} disabled={loadingMore}>
              {loadingMore && <LoaderCircleIcon data-icon="inline-start" className="size-3.5 animate-spin" />}
              {loadingMore ? t('skills.loadingMore') : t('skills.loadMore')}
            </Button>
          ) : (
            <p className="font-mono text-[10px] uppercase tracking-[0.14em] text-muted-foreground">{t('skills.endOfList')}</p>
          )}
        </div>
      </>
    )
  }

  if (items.length === 0) {
    return (
      <Card className="skill-library-empty border-dashed bg-card/70 p-0">
        <CardContent className="flex flex-col items-center justify-center px-6 py-14 text-center">
          <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
            <SearchXIcon className="size-6" />
          </div>
          <h2 className="serif mt-4 text-2xl font-semibold">{t('skills.emptyTitle')}</h2>
          <p className="mt-2 max-w-md text-sm leading-6 text-muted-foreground">{t('skills.emptyDescription')}</p>
        </CardContent>
      </Card>
    )
  }

  return (
    <>
      {error && (
        <Alert variant="destructive" className="mb-4">
          <AlertTitle>{t('skills.failedLoadMore')}</AlertTitle>
          <AlertDescription className="flex flex-wrap items-center gap-3">
            <span>{error}</span>
            <Button type="button" size="sm" variant="outline" onClick={onRetry}>{t('common.retry')}</Button>
          </AlertDescription>
        </Alert>
      )}
      <div className="skill-library-index__items" role="list" aria-label={t('skills.listHeading')}>
        {items.map((item) => (
          <div
            key={item.id}
            className={cn(
              'skill-library-index__row',
              item.id === selectedId && 'is-selected',
              item.id === highlightedId && 'is-highlighted',
            )}
            role="listitem"
          >
            <button
              type="button"
              className="skill-library-index__select"
              aria-current={item.id === selectedId ? 'true' : undefined}
              onClick={() => onSelect(item)}
            >
              <span className="skill-library-index__avatar" aria-hidden="true"><BookOpenIcon className="size-3.5" /></span>
              <span className="skill-library-index__copy">
                <strong>{item.name}</strong>
                <small>{item.domainName || t('skills.noDomain')} · {t(skillSourceKey(item.sourceType))}</small>
              </span>
              <Badge variant="outline" className="skill-library-index__version font-mono text-[9px]">
                {t('skills.version', { version: item.version })}
              </Badge>
              <ChevronRightIcon className="skill-library-index__chevron size-3.5" aria-hidden="true" />
            </button>
          </div>
        ))}
      </div>
      <div ref={sentinelRef} aria-hidden="true" className="h-2" />
      <div className="skill-library-pagination flex flex-col items-center justify-center gap-3 text-center">
        {hasMore ? (
          <Button type="button" variant="outline" onClick={onLoadMore} disabled={loadingMore}>
            {loadingMore && <LoaderCircleIcon data-icon="inline-start" className="size-3.5 animate-spin" />}
            {loadingMore ? t('skills.loadingMore') : t('skills.loadMore')}
          </Button>
        ) : (
          <p className="font-mono text-[10px] uppercase tracking-[0.14em] text-muted-foreground">{t('skills.endOfList')}</p>
        )}
      </div>
    </>
  )
}
