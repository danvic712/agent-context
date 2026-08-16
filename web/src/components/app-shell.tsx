import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ActivityIcon, BarChart3Icon, BookOpenIcon, ExternalLinkIcon, FolderArchiveIcon, FolderSearchIcon, SettingsIcon, WrenchIcon } from 'lucide-react'
import { ThemeToggle } from '@/components/theme-toggle'
import { AnalyticsOverview } from '@/components/analytics-overview'
import { EngineHealthView } from '@/components/engine-health'
import { KnowledgeManager } from '@/components/knowledge-manager'
import { SettingsPage } from '@/components/settings-page'
import { SkillManager } from '@/components/skill-manager'
import { getDashboardUrl, getHealth } from '@/lib/api'
import { cn } from '@/lib/utils'

type Tab = 'knowledge' | 'review' | 'archived' | 'skills' | 'analytics' | 'health' | 'settings'

const icons: Record<Tab, React.ReactNode> = {
  knowledge: <BookOpenIcon className="size-3.5" />,
  review: <FolderSearchIcon className="size-3.5" />,
  archived: <FolderArchiveIcon className="size-3.5" />,
  skills: <WrenchIcon className="size-3.5" />,
  analytics: <BarChart3Icon className="size-3.5" />,
  health: <ActivityIcon className="size-3.5" />,
  settings: <SettingsIcon className="size-3.5" />,
}

export function AppShell() {
  const { t } = useTranslation()
  const [tab, setTab] = useState<Tab>('knowledge')
  const [healthy, setHealthy] = useState<boolean | null>(null)
  const [dashboardUrl, setDashboardUrl] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getHealth()
      .then((health) => {
        if (!cancelled) setHealthy(health.database === 'ok')
      })
      .catch(() => {
        if (!cancelled) setHealthy(false)
      })
    getDashboardUrl()
      .then((dto) => {
        if (!cancelled) setDashboardUrl(dto.url)
      })
      .catch(() => {
        // Dashboard URL is optional — the entry stays hidden when unconfigured.
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

  const page =
    tab === 'skills' ? (
      <SkillManager />
    ) : tab === 'analytics' ? (
      <AnalyticsOverview />
    ) : tab === 'health' ? (
      <EngineHealthView />
    ) : tab === 'settings' ? (
      <SettingsPage />
    ) : (
      <KnowledgeManager mode={tab === 'review' ? 'review' : tab === 'archived' ? 'archived' : 'all'} />
    )

  return (
    <div className="flex min-h-svh flex-col">
      {/* Topbar navigation (Botanical) */}
      <header
        className="sticky top-0 z-20 border-b backdrop-blur"
        style={{ borderColor: 'var(--line)', background: 'var(--topbg)' }}
      >
        <div className="flex items-center gap-3 px-4 py-2.5 md:px-6">
          {/* Brand */}
          <div className="flex shrink-0 items-center gap-2.5">
            <div
              className="flex size-7 items-center justify-center rounded-[11px] text-[14px]"
              style={{ background: 'var(--accent)', boxShadow: '0 4px 12px var(--accent-shadow)', border: '1px solid var(--line2)' }}
            >
              🌿
            </div>
            <div className="hidden sm:block">
              <div className="serif text-[16px] font-semibold leading-none">{t('appShell.title')}</div>
              <div className="mt-1 font-mono text-[7.5px] uppercase tracking-[0.14em] text-muted-foreground">
                CONTEXT LAYER
              </div>
            </div>
          </div>

          {/* Nav pills */}
          <nav className="flex min-w-0 flex-1 items-center gap-1 overflow-x-auto">
            {tabs.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => setTab(item.id)}
                aria-current={tab === item.id ? 'page' : undefined}
                className={cn(
                  'flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full px-3 py-1.5 text-[12.5px] font-medium transition-all duration-150',
                  tab === item.id
                    ? 'text-primary-foreground'
                    : 'text-muted-foreground hover:bg-[var(--hover)] hover:text-foreground',
                )}
                style={tab === item.id ? { background: 'var(--accent)', boxShadow: '0 3px 10px var(--accent-shadow)' } : undefined}
              >
                {icons[item.id]}
                {item.label}
              </button>
            ))}
          </nav>

          {/* Status + dashboard + theme */}
          <div className="flex shrink-0 items-center gap-2">
            {dashboardUrl && (
              <button
                type="button"
                onClick={() => window.open(dashboardUrl, '_blank', 'noopener,noreferrer')}
                title={dashboardUrl}
                className="flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-1.5 text-[11.5px] font-medium text-muted-foreground transition-all duration-150 hover:text-foreground"
                style={{ borderColor: 'var(--line)', background: 'var(--card2-paper)' }}
              >
                <ExternalLinkIcon className="size-3.5" />
                {t('appShell.dashboard')}
              </button>
            )}
            <div className="hidden items-center gap-1.5 rounded-full border px-2.5 py-1 font-mono text-[9.5px] text-muted-foreground lg:flex" style={{ borderColor: 'var(--line)', background: 'var(--card2-paper)' }}>
              <span
                className={cn(
                  'inline-block size-1.5 rounded-full',
                  healthy === null ? 'bg-warn' : healthy ? 'bg-ok shadow-[0_0_5px_var(--ok)]' : 'bg-destructive',
                )}
              />
              {healthy === null ? t('appShell.checking') : healthy ? 'OK' : t('appShell.degraded')}
            </div>
            <ThemeToggle />
          </div>
        </div>
      </header>

      {/* Content — keyed so tab switches animate in */}
      <main key={tab} className="animate-in fade-in slide-in-from-bottom-1 duration-200 flex-1 p-4 md:p-6">
        {page}
      </main>
    </div>
  )
}
