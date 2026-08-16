import { useEffect, useState } from 'react'
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
      setError(cause instanceof Error ? cause.message : 'Failed to load knowledge')
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
      setError(cause instanceof Error ? cause.message : 'Failed to update item')
    }
  }

  const restore = async (item: KnowledgeItem) => {
    setError(null)
    try {
      await restoreKnowledge(item.id)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Failed to restore item')
    }
  }

  const remove = async (item: KnowledgeItem) => {
    if (!window.confirm(`Delete "${item.title}"? This cannot be undone.`)) {
      return
    }
    setError(null)
    try {
      await deleteKnowledge(item.id)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Failed to delete item')
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {mode === 'review' && threshold !== null && (
        <p className="text-sm text-muted-foreground">
          Items below the Confidence threshold ({threshold}) — candidates for review.
        </p>
      )}

      {mode === 'archived' && (
        <p className="text-sm text-muted-foreground">
          Archived by hygiene — restore them back to Active or delete permanently.
        </p>
      )}

      {error && <p className="text-sm text-destructive">{error}</p>}

      {loading ? (
        <p className="text-sm text-muted-foreground">Loading…</p>
      ) : items.length === 0 ? (
        <Card>
          <CardContent className="pt-6 text-sm text-muted-foreground">
            {mode === 'all' && 'No knowledge yet. Report sessions through Craft Agents and the Learning Engine will distill them here.'}
            {mode === 'review' && 'Nothing below the Confidence threshold — the knowledge base is healthy.'}
            {mode === 'archived' && 'Nothing archived — hygiene has not moved any items yet.'}
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
                      private
                    </Badge>
                  )}
                </div>
              </div>
              <CardDescription className="line-clamp-2">{item.content}</CardDescription>
            </CardHeader>
            <CardContent className="flex items-center justify-between gap-4">
              <p className="text-xs text-muted-foreground">
                {item.domainName ?? 'no domain'}
                {item.sourceSessionTask ? ` · from “${item.sourceSessionTask}”` : ''}
              </p>
              <div className="flex shrink-0 items-center gap-2">
                {mode === 'archived' ? (
                  <Button variant="outline" size="sm" onClick={() => void restore(item)}>
                    Restore
                  </Button>
                ) : (
                  <Button variant="outline" size="sm" onClick={() => void togglePrivate(item)}>
                    {item.isPrivate ? 'Unmark private' : 'Mark private'}
                  </Button>
                )}
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => void remove(item)}
                  aria-label={`Delete ${item.title}`}
                >
                  <TrashIcon data-icon="inline-start" className="size-4" />
                  Delete
                </Button>
              </div>
            </CardContent>
          </Card>
        ))
      )}
    </div>
  )
}
