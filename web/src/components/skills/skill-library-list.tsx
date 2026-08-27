import { LoaderCircleIcon, RefreshCwIcon, SearchXIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { SkillCard } from './skill-card'
import type { SkillItem } from '@/lib/api'

interface SkillLibraryListProps {
  items: SkillItem[]
  loading: boolean
  loadingMore: boolean
  hasMore: boolean
  error: string | null
  highlightedId: string | null
  filterActive?: boolean
  sentinelRef?: (node: HTMLDivElement | null) => void
  onLoadMore: () => void
  onRetry: () => void
  onClearFilter?: () => void
  deletingId?: string | null
  onDelete: (item: SkillItem) => void
}

export function SkillLibraryList({
  items,
  loading,
  loadingMore,
  hasMore,
  error,
  highlightedId,
  filterActive = false,
  sentinelRef,
  onLoadMore,
  onRetry,
  onClearFilter,
  deletingId = null,
  onDelete,
}: SkillLibraryListProps) {
  const { t } = useTranslation()

  if (loading) {
    return (
      <div className="skill-library-card-grid" aria-busy="true" aria-label={t('skills.loading')}>
        {Array.from({ length: 6 }, (_, index) => (
          <Card key={index} className="skill-library-card skill-library-card--skeleton p-0">
            <div className="flex items-start gap-3">
              <Skeleton className="size-10 rounded-xl" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-2/3" />
                <Skeleton className="h-3 w-1/2" />
              </div>
            </div>
            <Skeleton className="mt-5 h-10 w-full" />
            <Skeleton className="mt-5 h-7 w-full" />
          </Card>
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
      <div className="skill-library-card-grid">
        {items.map((item) => (
          <SkillCard
            key={item.id}
            item={item}
            highlighted={item.id === highlightedId}
            deleting={item.id === deletingId}
            onDelete={() => onDelete(item)}
          />
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
