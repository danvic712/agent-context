import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BarChart3Icon, CoinsIcon, PlusIcon, TrashIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import {
  deletePricing,
  getOverview,
  listPricing,
  savePricing,
  type AnalyticsGroupItem,
  type AnalyticsOverview,
  type ModelPricing,
} from '@/lib/api'

const money = (value: number) => `$${value.toFixed(4)}`
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
                <span className="font-medium text-foreground">{money(item.cost)}</span>
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
  const [pricing, setPricing] = useState<ModelPricing[]>([])
  const [filterDomain, setFilterDomain] = useState('')
  const [filterAgent, setFilterAgent] = useState('')
  const [error, setError] = useState<string | null>(null)
  // New pricing row form
  const [newModel, setNewModel] = useState('')
  const [newIn, setNewIn] = useState('')
  const [newOut, setNewOut] = useState('')

  const load = async () => {
    setError(null)
    try {
      const [ov, prices] = await Promise.all([
        getOverview({ domain: filterDomain || undefined, agent: filterAgent || undefined }),
        listPricing(),
      ])
      setOverview(ov)
      setPricing(prices)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('analytics.failedLoad'))
    }
  }

  useEffect(() => {
    void load()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterDomain, filterAgent])

  const addPricing = async () => {
    setError(null)
    try {
      await savePricing(newModel.trim(), Number(newIn), Number(newOut))
      setNewModel('')
      setNewIn('')
      setNewOut('')
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('analytics.failedSavePricing'))
    }
  }

  const removePricing = async (model: string) => {
    setError(null)
    try {
      await deletePricing(model)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('analytics.failedDeletePricing'))
    }
  }

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

          {overview && (
            <div className="flex flex-wrap items-center gap-3">
              <Badge variant="default">
                {t('analytics.sessions', { count: overview.totalSessions })}
              </Badge>
              <Badge variant="secondary">
                {t('analytics.tokens', { count: tokens(overview.totalTokensIn + overview.totalTokensOut) })}
              </Badge>
              <Badge variant="default">
                <CoinsIcon data-icon="inline-start" className="size-3" />
                {money(overview.totalCost)}
              </Badge>
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

      <Card>
        <CardHeader>
          <CardTitle className="text-base">{t('analytics.pricingTitle')}</CardTitle>
          <CardDescription>{t('analytics.pricingDescription')}</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <div className="flex flex-wrap items-end gap-3">
            <Field>
              <FieldLabel>{t('analytics.model')}</FieldLabel>
              <Input
                value={newModel}
                onChange={(e) => setNewModel(e.target.value)}
                placeholder={t('analytics.modelPlaceholder')}
              />
            </Field>
            <Field>
              <FieldLabel>{t('analytics.inputPerToken')}</FieldLabel>
              <Input
                value={newIn}
                onChange={(e) => setNewIn(e.target.value)}
                placeholder="0.0000025"
                type="number"
                step="0.0000001"
                min="0"
              />
            </Field>
            <Field>
              <FieldLabel>{t('analytics.outputPerToken')}</FieldLabel>
              <Input
                value={newOut}
                onChange={(e) => setNewOut(e.target.value)}
                placeholder="0.00001"
                type="number"
                step="0.0000001"
                min="0"
              />
            </Field>
            <Button size="sm" onClick={() => void addPricing()}>
              <PlusIcon data-icon="inline-start" className="size-4" />
              {t('analytics.addOrUpdate')}
            </Button>
          </div>

          <div className="flex flex-col gap-2">
            {pricing.length === 0 ? (
              <p className="text-sm text-muted-foreground">{t('analytics.pricingEmpty')}</p>
            ) : (
              pricing.map((row) => (
                <div
                  key={row.id}
                  className="flex items-center justify-between gap-4 text-sm"
                >
                  <span className="font-mono font-medium">{row.model}</span>
                  <div className="flex items-center gap-3 text-muted-foreground">
                    <span>{t('analytics.inPerToken', { value: row.inputCostPerToken })}</span>
                    <span>{t('analytics.outPerToken', { value: row.outputCostPerToken })}</span>
                    <Button
                      variant="destructive"
                      size="sm"
                      onClick={() => void removePricing(row.model)}
                      aria-label={t('analytics.deletePricingAria', { model: row.model })}
                    >
                      <TrashIcon data-icon="inline-start" className="size-4" />
                      {t('common.delete')}
                    </Button>
                  </div>
                </div>
              ))
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  )
}
