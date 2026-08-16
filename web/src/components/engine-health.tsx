import { useEffect, useState } from 'react'
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
import { getEngineHealth, runHygiene, type EngineHealth, type HygieneResult } from '@/lib/api'

export function EngineHealthView() {
  const [health, setHealth] = useState<EngineHealth | null>(null)
  const [hygiene, setHygiene] = useState<HygieneResult | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = async () => {
    setError(null)
    try {
      setHealth(await getEngineHealth())
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Failed to load engine health')
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
      setError(cause instanceof Error ? cause.message : 'Failed to run hygiene')
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <ActivityIcon className="size-4 text-muted-foreground" />
            Learning Engine
          </CardTitle>
          <CardDescription>
            Queue depth and retry visibility from the Postgres-as-queue sessions table (US29).
          </CardDescription>
        </CardHeader>
        <CardContent>
          {health && (
            <div className="flex flex-wrap items-center gap-3">
              <Badge variant="default">{health.queuedSessions} queued</Badge>
              <Badge variant="secondary">{health.processingSessions} processing</Badge>
              <Badge variant={health.failedSessions > 0 ? 'destructive' : 'outline'}>
                {health.failedSessions} failed
              </Badge>
              <Badge variant={health.retryScheduledSessions > 0 ? 'default' : 'outline'}>
                {health.retryScheduledSessions} retry scheduled
              </Badge>
              <Badge variant="outline">{health.totalSessions} total sessions</Badge>
            </div>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <SparklesIcon className="size-4 text-muted-foreground" />
            Knowledge hygiene
          </CardTitle>
          <CardDescription>
            Decays long-unused Knowledge and moves decayed items to Review, then
            Archives untouched Review items (US20). Runs on a timer — trigger it now.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div>
            <Button size="sm" onClick={() => void run()}>
              <RefreshCwIcon data-icon="inline-start" className="size-4" />
              Run hygiene now
            </Button>
          </div>
          {hygiene && (
            <div className="flex flex-wrap items-center gap-2 text-sm">
              <Badge variant="secondary">{hygiene.decayed} decayed</Badge>
              <Badge variant="secondary">{hygiene.movedToReview} moved to review</Badge>
              <Badge variant="secondary">{hygiene.archived} archived</Badge>
            </div>
          )}
        </CardContent>
      </Card>

      {error && <p className="text-sm text-destructive">{error}</p>}
    </div>
  )
}
