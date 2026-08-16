import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { CircleCheckIcon, DatabaseIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { AnalyticsOverview } from '@/components/analytics-overview'
import { EngineHealthView } from '@/components/engine-health'
import { KnowledgeManager } from '@/components/knowledge-manager'
import { SettingsPage } from '@/components/settings-page'
import { SkillManager } from '@/components/skill-manager'
import { getHealth } from '@/lib/api'

type Tab = 'knowledge' | 'review' | 'archived' | 'skills' | 'analytics' | 'health' | 'settings'

export function AppShell() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('knowledge')
  const [healthy, setHealthy] = useState<boolean | null>(null)

  useEffect(() => {
    let cancelled = false
    getHealth()
      .then((health) => {
        if (!cancelled) setHealthy(health.database === 'ok')
      })
      .catch(() => {
        if (!cancelled) setHealthy(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  const tabs: { id: Tab; label: string }[] = [
    { id: 'knowledge', label: t('appShell.tabs.knowledge') },
    { id: 'review', label: t('appShell.tabs.review') },
    { id: 'archived', label: t('appShell.tabs.archived') },
    { id: 'skills', label: t('appShell.tabs.skills') },
    { id: 'analytics', label: t('appShell.tabs.analytics') },
    { id: 'health', label: t('appShell.tabs.health') },
    { id: 'settings', label: t('appShell.tabs.settings') },
  ]

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between border-b px-6 py-3">
        <div className="flex items-center gap-2 font-semibold">
          <DatabaseIcon data-icon="inline-start" />
          {t('appShell.title')}
        </div>
        {healthy === null ? (
          <Badge variant="secondary">{t('appShell.checking')}</Badge>
        ) : healthy ? (
          <Badge variant="default">
            <CircleCheckIcon data-icon="inline-start" />
            {t('appShell.healthy')}
          </Badge>
        ) : (
          <Badge variant="destructive">{t('appShell.degraded')}</Badge>
        )}
      </header>
      <main className="flex flex-1 flex-col gap-6 p-6">
        <nav className="flex items-center gap-2">
          {tabs.map((item) => (
            <Button
              key={item.id}
              variant={tab === item.id ? 'default' : 'outline'}
              size="sm"
              onClick={() => setTab(item.id)}
            >
              {item.label}
            </Button>
          ))}
        </nav>
        {tab === 'skills' ? (
          <SkillManager />
        ) : tab === 'analytics' ? (
          <AnalyticsOverview />
        ) : tab === 'health' ? (
          <EngineHealthView />
        ) : tab === 'settings' ? (
          <SettingsPage />
        ) : (
          <KnowledgeManager
            mode={tab === 'review' ? 'review' : tab === 'archived' ? 'archived' : 'all'}
          />
        )}
      </main>
    </div>
  )
}
