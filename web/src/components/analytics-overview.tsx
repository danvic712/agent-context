import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BarChart3Icon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import {
  getOverview,
  type AnalyticsGroupItem,
  type AnalyticsOverview,
} from '@/lib/api'

const tokens = (value: number) => value.toLocaleString('en-US')

function GroupTable({ title, items, noSessions, sessionsLabel }: {
  title: string
  items: AnalyticsGroupItem[]
  noSessions: string
  sessionsLabel: (count: number) => string
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm">{title}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {items.length === 0 ? (
          <p className="text-sm text-muted-foreground">{noSessions}</p>
        ) : (
          items.map((item) => (
            <div key={item.name} className="flex items-center justify-between gap-4 text-sm">
              <span className="font-medium">{item.name}</span>
              <div className="flex items-center gap-3 text-muted-foreground">
                <span>{sessionsLabel(item.sessions)}</span>
                <span>{tokens(item.tokensIn + item.tokensOut)}</span>
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  )
}

export function AnalyticsOverview() {
  const { t } = useTranslation()
  const [overview, setOverview] = useState<AnalyticsOverview | null>(null)
  const [filterDomain, setFilterDomain] = useState('')
  const [filterAgent, setFilterAgent] = useState('')
  const [error, setError] = useState<string | null>(null)

  const load = async () => {
    setError(null)
    try {
      const ov = await getOverview({ domain: filterDomain || undefined, agent: filterAgent || undefined })
      setOverview(ov)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('analytics.failedLoad'))
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterDomain, filterAgent])

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <BarChart3Icon className="size-4 text-muted-foreground" />
            {t('analytics.overviewTitle')}
          </CardTitle>
          <CardDescription>{t('analytics.overviewDescription')}</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="mb-4 flex items-end gap-3">
            <Field>
              <FieldLabel>{t('analytics.domain')}</FieldLabel>
              <Input
                value={filterDomain}
                onChange={(e) => setFilterDomain(e.target.value)}
                placeholder={t('analytics.domainPlaceholder')}
              />
            </Field>
            <Field>
              <FieldLabel>{t('analytics.agent')}</FieldLabel>
              <Input
                value={filterAgent}
                onChange={(e) => setFilterAgent(e.target.value)}
                placeholder={t('analytics.agentPlaceholder')}
              />
            </Field>
          </div>

          {overview ? (
            <div className="flex flex-wrap items-center gap-3">
              <Badge variant="default">
                {t('analytics.sessions', { count: overview.totalSessions })}
              </Badge>
              <Badge variant="secondary">
                {t('analytics.tokens', { count: tokens(overview.totalTokensIn + overview.totalTokensOut) })}
              </Badge>
            </div>
          ) : (
            <div className="flex flex-wrap items-center gap-3" aria-busy="true">
              <Skeleton className="h-6 w-24" />
              <Skeleton className="h-6 w-28" />
            </div>
          )}
        </CardContent>
      </Card>

      {error && <p className="text-sm text-destructive">{error}</p>}

      {overview && (
        <div className="grid gap-4 md:grid-cols-2">
          <GroupTable
            title={t('analytics.byDomain')}
            items={overview.byDomain}
            noSessions={t('analytics.noSessions')}
            sessionsLabel={(count) => t('analytics.sessions', { count })}
          />
          <GroupTable
            title={t('analytics.byAgent')}
            items={overview.byAgent}
            noSessions={t('analytics.noSessions')}
            sessionsLabel={(count) => t('analytics.sessions', { count })}
          />
        </div>
      )}
    </div>
  )
}
