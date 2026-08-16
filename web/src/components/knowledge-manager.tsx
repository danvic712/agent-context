import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ArchiveIcon, BookOpenIcon, LockIcon, TrashIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  deleteKnowledge,
  listArchivedKnowledge,
  listKnowledge,
  listReviewKnowledge,
  restoreKnowledge,
  setKnowledgePrivate,
  type KnowledgeItem,
} from '@/lib/api'

export type KnowledgeMode = 'all' | 'review' | 'archived'

interface KnowledgeManagerProps {
  mode: KnowledgeMode
}

export function KnowledgeManager({ mode }: KnowledgeManagerProps) {
  const { t } = useTranslation()
  const [items, setItems] = useState<KnowledgeItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [threshold, setThreshold] = useState<number | null>(null)

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      if (mode === 'all') {
        setItems(await listKnowledge())
        setThreshold(null)
      } else if (mode === 'review') {
        const review = await listReviewKnowledge()
        setItems(review.items)
        setThreshold(review.threshold) // the backend owns the threshold — no hardcoding
      } else {
        setItems(await listArchivedKnowledge())
        setThreshold(null)
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('knowledge.failedLoad'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode])

  const togglePrivate = async (item: KnowledgeItem) => {
    setError(null)
    try {
      await setKnowledgePrivate(item.id, !item.isPrivate)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('knowledge.failedUpdate'))
    }
  }

  const restore = async (item: KnowledgeItem) => {
    setError(null)
    try {
      await restoreKnowledge(item.id)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('knowledge.failedRestore'))
    }
  }

  const remove = async (item: KnowledgeItem) => {
    if (!window.confirm(t('knowledge.deleteConfirm', { title: item.title }))) {
      return
    }
    setError(null)
    try {
      await deleteKnowledge(item.id)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('knowledge.failedDelete'))
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {mode === 'review' && threshold !== null && (
        <p className="text-sm text-muted-foreground">
          {t('knowledge.reviewThresholdNote', { threshold })}
        </p>
      )}

      {mode === 'archived' && (
        <p className="text-sm text-muted-foreground">{t('knowledge.archivedNote')}</p>
      )}

      {error && <p className="text-sm text-destructive">{error}</p>}

      {loading ? (
        <div className="flex flex-col gap-4" aria-busy="true">
          {[0, 1, 2].map((i) => (
            <Card key={i}>
              <CardHeader className="flex flex-row items-start justify-between gap-4">
                <div className="flex flex-col gap-2">
                  <Skeleton className="h-4 w-48" />
                  <Skeleton className="h-3 w-64" />
                </div>
                <Skeleton className="h-6 w-24" />
              </CardHeader>
              <CardContent>
                <Skeleton className="h-4 w-40" />
              </CardContent>
            </Card>
          ))}
        </div>
      ) : items.length === 0 ? (
        <Card>
          <CardContent className="pt-6 text-sm text-muted-foreground">
            {mode === 'all' && t('knowledge.emptyAll')}
            {mode === 'review' && t('knowledge.emptyReview')}
            {mode === 'archived' && t('knowledge.emptyArchived')}
          </CardContent>
        </Card>
      ) : (
        items.map((item) => (
          <Card key={item.id}>
            <CardHeader>
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-center gap-2">
                  {mode === 'archived' ? (
                    <ArchiveIcon className="size-4 text-muted-foreground" />
                  ) : (
                    <BookOpenIcon className="size-4 text-muted-foreground" />
                  )}
                  <CardTitle className="text-base">{item.title}</CardTitle>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <Badge variant={item.type === 'Solution' ? 'default' : 'secondary'}>
                    {item.type}
                  </Badge>
                  <Badge variant="default">{(item.confidence * 100).toFixed(0)}%</Badge>
                  {item.isPrivate && (
                    <Badge variant="outline">
                      <LockIcon data-icon="inline-start" className="size-3" />
                      {t('knowledge.private')}
                    </Badge>
                  )}
                </div>
              </div>
              <CardDescription className="line-clamp-2">{item.content}</CardDescription>
            </CardHeader>
            <CardContent className="flex items-center justify-between gap-4">
              <p className="text-xs text-muted-foreground">
                {item.domainName ?? t('knowledge.noDomain')}
                {item.sourceSessionTask ? ` · ${t('knowledge.fromTask', { task: item.sourceSessionTask })}` : ''}
              </p>
              <div className="flex shrink-0 items-center gap-2">
                {mode === 'archived' ? (
                  <Button variant="outline" size="sm" onClick={() => void restore(item)}>
                    {t('knowledge.restore')}
                  </Button>
                ) : (
                  <Button variant="outline" size="sm" onClick={() => void togglePrivate(item)}>
                    {item.isPrivate ? t('knowledge.unmarkPrivate') : t('knowledge.markPrivate')}
                  </Button>
                )}
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => void remove(item)}
                  aria-label={t('knowledge.deleteAria', { title: item.title })}
                >
                  <TrashIcon data-icon="inline-start" className="size-4" />
                  {t('common.delete')}
                </Button>
              </div>
            </CardContent>
          </Card>
        ))
      )}
    </div>
  )
}
