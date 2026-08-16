import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ActivityIcon, RefreshCwIcon, SparklesIcon } from 'lucide-react'
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
import { getEngineHealth, runHygiene, type EngineHealth, type HygieneResult } from '@/lib/api'

export function EngineHealthView() {
  const { t } = useTranslation()
  const [health, setHealth] = useState<EngineHealth | null>(null)
  const [hygiene, setHygiene] = useState<HygieneResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = async () => {
    setError(null)
    try {
      setHealth(await getEngineHealth())
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('engineHealth.failedLoad'))
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const run = async () => {
    setError(null)
    setHygiene(null)
    try {
      setHygiene(await runHygiene())
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('engineHealth.failedRun'))
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ActivityIcon className="size-4 text-muted-foreground" />
            {t('engineHealth.learningEngineTitle')}
          </CardTitle>
          <CardDescription>{t('engineHealth.learningEngineDescription')}</CardDescription>
        </CardHeader>
        <CardContent>
          {health ? (
            <div className="flex flex-wrap items-center gap-3">
              <Badge variant="default">
                {t('engineHealth.queued', { count: health.queuedSessions })}
              </Badge>
              <Badge variant="secondary">
                {t('engineHealth.processing', { count: health.processingSessions })}
              </Badge>
              <Badge variant={health.failedSessions > 0 ? 'destructive' : 'outline'}>
                {t('engineHealth.failed', { count: health.failedSessions })}
              </Badge>
              <Badge variant={health.retryScheduledSessions > 0 ? 'default' : 'outline'}>
                {t('engineHealth.retryScheduled', { count: health.retryScheduledSessions })}
              </Badge>
              <Badge variant="outline">
                {t('engineHealth.totalSessions', { count: health.totalSessions })}
              </Badge>
            </div>
          ) : (
            <div className="flex flex-wrap items-center gap-3" aria-busy="true">
              <Skeleton className="h-6 w-20" />
              <Skeleton className="h-6 w-24" />
              <Skeleton className="h-6 w-16" />
              <Skeleton className="h-6 w-28" />
              <Skeleton className="h-6 w-24" />
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <SparklesIcon className="size-4 text-muted-foreground" />
            {t('engineHealth.hygieneTitle')}
          </CardTitle>
          <CardDescription>{t('engineHealth.hygieneDescription')}</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div>
            <Button size="sm" onClick={() => void run()}>
              <RefreshCwIcon data-icon="inline-start" className="size-4" />
              {t('engineHealth.runHygiene')}
            </Button>
          </div>
          {hygiene && (
            <div className="flex flex-wrap items-center gap-2 text-sm">
              <Badge variant="secondary">{t('engineHealth.decayed', { count: hygiene.decayed })}</Badge>
              <Badge variant="secondary">
                {t('engineHealth.movedToReview', { count: hygiene.movedToReview })}
              </Badge>
              <Badge variant="secondary">
                {t('engineHealth.archived', { count: hygiene.archived })}
              </Badge>
            </div>
          )}
        </CardContent>
      </Card>

      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
