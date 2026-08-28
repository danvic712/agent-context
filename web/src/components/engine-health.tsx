import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ActivityIcon, CheckIcon, RefreshCwIcon, SparklesIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { EngineMetricsSkeleton } from '@/components/ui/loading-skeletons'
import { Surface } from '@/components/ui/surface'
import { getEngineHealth, getUserFacingError, runHygiene, type EngineHealth, type HygieneResult } from '@/lib/api'
import { getEngineHealthState } from '@/lib/engine-health-state'

type EngineError = {
  message: string
  source: 'load' | 'run'
}

interface EngineHealthPanelProps {
  className?: string
}

export function EngineHealthPanel({ className }: EngineHealthPanelProps) {
  const { t } = useTranslation()
  const [health, setHealth] = useState<EngineHealth | null>(null)
  const [hygiene, setHygiene] = useState<HygieneResult | null>(null)
  const [loading, setLoading] = useState(true)
  const [runningHygiene, setRunningHygiene] = useState(false)
  const [error, setError] = useState<EngineError | null>(null)

  const load = useCallback(async (showLoading = true) => {
    if (showLoading) setLoading(true)
    setError(null)
    try {
      setHealth(await getEngineHealth())
    } catch (cause) {
      setError({
        source: 'load',
        message: getUserFacingError(cause, t('engineHealth.failedLoad')),
      })
    } finally {
      if (showLoading) setLoading(false)
    }
  }, [t])

  useEffect(() => {
    void load()
  }, [load])

  const run = async () => {
    setError(null)
    setHygiene(null)
    setRunningHygiene(true)
    try {
      setHygiene(await runHygiene())
      await load(false)
    } catch (cause) {
      setError({
        source: 'run',
        message: getUserFacingError(cause, t('engineHealth.failedRun')),
      })
    } finally {
      setRunningHygiene(false)
    }
  }

  const state = getEngineHealthState(health, Boolean(error) && !health)
  const attention = state === 'attention'
  const statusTitle = {
    loading: t('engineHealth.statusChecking'),
    healthy: t('engineHealth.statusHealthy'),
    attention: t('engineHealth.statusAttention'),
    degraded: t('engineHealth.statusDegraded'),
  }[state]
  const statusDetail = health
    ? attention
      ? t('engineHealth.statusAttentionDetail', {
          failed: health.failedSessions,
          retry: health.retryScheduledSessions,
        })
      : t('engineHealth.statusHealthyDetail', { queued: health.queuedSessions })
    : state === 'loading'
      ? t('engineHealth.statusCheckingDetail')
      : t('engineHealth.statusDegradedDetail')

  return (
    <Surface
      as="section"
      className={`c-panel c-engine-panel c-settings-anchor${className ? ` ${className}` : ''}`}
      data-engine-state={state}
      aria-labelledby="engine-health-title"
    >
      <div className="c-panel__header c-engine-panel__header">
        <div className="c-engine-heading">
          <span className="c-engine-heading__icon"><ActivityIcon /></span>
          <div>
            <h3 id="engine-health-title" className="c-panel__title">
              {t('engineHealth.learningEngineTitle')}
            </h3>
            <p className="c-panel__description">{t('engineHealth.learningEngineDescription')}</p>
          </div>
        </div>
        <div
          className={`c-engine-summary c-engine-summary--${state}`}
          role="status"
          aria-live="polite"
        >
          <span className="c-engine-summary__dot" />
          <div>
            <strong>{statusTitle}</strong>
            <small>{statusDetail}</small>
          </div>
        </div>
      </div>

      <div className="c-panel__body c-engine-panel__body" aria-busy={loading}>
        {loading && !health ? (
          <EngineMetricsSkeleton label={t('engineHealth.statusChecking')} />
        ) : health ? (
          <>
            <div className="c-engine-metrics">
              <EngineMetric label={t('engineHealth.metricQueued')} value={health.queuedSessions} note={t('engineHealth.queuedNote', { count: health.queuedSessions })} tone={health.queuedSessions === 0 ? 'ok' : undefined} />
              <EngineMetric label={t('engineHealth.metricProcessing')} value={health.processingSessions} note={t('engineHealth.processingNote', { count: health.processingSessions })} />
              <EngineMetric label={t('engineHealth.metricFailed')} value={health.failedSessions} note={health.failedSessions > 0 ? t('engineHealth.failedAttention') : t('engineHealth.failedNone')} tone={health.failedSessions > 0 ? 'warn' : 'ok'} />
              <EngineMetric label={t('engineHealth.metricRetryScheduled')} value={health.retryScheduledSessions} note={health.retryScheduledSessions > 0 ? t('engineHealth.retryAttention') : t('engineHealth.retryNone')} tone={health.retryScheduledSessions > 0 ? 'warn' : 'ok'} />
              <EngineMetric label={t('engineHealth.metricTotal')} value={health.totalSessions} note={t('engineHealth.totalNote')} />
            </div>
            {health.totalSessions === 0 && (
              <div className="c-engine-empty" role="status">
                <SparklesIcon />
                <span>{t('engineHealth.emptyState')}</span>
              </div>
            )}
          </>
        ) : null}

        {error && (
          <Alert variant="destructive" className="c-engine-alert">
            <AlertTitle>{t(error.source === 'run' ? 'engineHealth.failedRunTitle' : 'engineHealth.failedLoadTitle')}</AlertTitle>
            <AlertDescription>
              <span>{error.message}</span>
              <Button type="button" variant="outline" size="sm" onClick={() => void load()}>
                <RefreshCwIcon data-icon="inline-start" />
                {t('common.retry')}
              </Button>
            </AlertDescription>
          </Alert>
        )}

        <div className="c-engine-footer">
          <div className="c-engine-hygiene-copy">
            <SparklesIcon />
            <div>
              <strong>{t('engineHealth.hygieneTitle')}</strong>
              <span>{t('engineHealth.hygieneDescription')}</span>
            </div>
          </div>
          <div className="c-engine-action">
            <span className="c-engine-last-check">{t('engineHealth.lastChecked')}</span>
            <Button type="button" variant="outline" size="sm" onClick={() => void run()} disabled={runningHygiene}>
              <RefreshCwIcon data-icon="inline-start" className={runningHygiene ? 'animate-spin' : undefined} />
              {runningHygiene ? t('engineHealth.runningHygiene') : t('engineHealth.runHygiene')}
            </Button>
          </div>
        </div>

        {hygiene && (
          <div className="c-engine-result" role="status" aria-live="polite">
            <CheckIcon />
            <span>{t('engineHealth.actionResult', { ...hygiene })}</span>
          </div>
        )}
      </div>
    </Surface>
  )
}

function EngineMetric({
  label,
  value,
  note,
  tone,
}: {
  label: string
  value: number
  note: string
  tone?: 'ok' | 'warn'
}) {
  return (
    <div className="c-engine-metric">
      <div className="c-engine-metric__label">{label}</div>
      <div className={`c-engine-metric__value${tone ? ` c-engine-metric__value--${tone}` : ''}`}>{value}</div>
      <div className="c-engine-metric__note">{note}</div>
    </div>
  )
}
