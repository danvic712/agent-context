import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ActivityIcon, BarChart3Icon, BookOpenIcon, FolderArchiveIcon, FolderSearchIcon, SettingsIcon, WrenchIcon } from 'lucide-react'
import { ThemeToggle } from '@/components/theme-toggle'
import { AnalyticsOverview } from '@/components/analytics-overview'
import { EngineHealthView } from '@/components/engine-health'
import { KnowledgeManager } from '@/components/knowledge-manager'
import { SettingsPage } from '@/components/settings-page'
import { SkillManager } from '@/components/skill-manager'
import { getHealth } from '@/lib/api'
import { cn } from '@/lib/utils'

type Tab = 'knowledge' | 'review' | 'archived' | 'skills' | 'analytics' | 'health' | 'settings'

const icons: Record<Tab, React.ReactNode> = {
  knowledge: <BookOpenIcon className="size-4" />,
  review: <FolderSearchIcon className="size-4" />,
  archived: <FolderArchiveIcon className="size-4" />,
  skills: <WrenchIcon className="size-4" />,
  analytics: <BarChart3Icon className="size-4" />,
  health: <ActivityIcon className="size-4" />,
  settings: <SettingsIcon className="size-4" />,
}

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
    <div className="flex min-h-svh flex-col md:flex-row">
      {/* Sidebar (Field Notes) — becomes a top tab strip below md */}
      <aside className="hidden w-[232px] shrink-0 flex-col gap-0.5 px-3 py-4 md:flex" style={{ borderRight: '1px solid var(--line)' }}>
        <div className="mb-3 flex flex-col px-2 pb-4" style={{ borderBottom: '1px solid var(--line)' }}>
          <div className="serif text-[21px] font-semibold tracking-tight">{t('appShell.title')}</div>
          <div className="mt-0.5 font-mono text-[8px] uppercase tracking-[0.18em] text-muted-foreground">
            CONTEXT LAYER · MODEL v2.6
          </div>
        </div>

        <p className="px-2 pb-1 pt-2 font-mono text-[9px] uppercase tracking-[0.18em] text-muted-foreground">
          {t('appShell.workspace')}
        </p>

        <nav className="flex flex-col gap-0.5">
          {tabs.map((item, i) => (
            <button
              key={item.id}
              type="button"
              onClick={() => setTab(item.id)}
              aria-current={tab === item.id ? 'page' : undefined}
              className={cn(
                'relative flex items-center gap-2.5 rounded px-2.5 py-2 text-[13px] font-medium text-muted-foreground transition-colors',
                tab === item.id && 'text-foreground',
              )}
            >
              {tab === item.id && (
                <span className="absolute -left-3 bottom-1 top-1 w-[3px] rounded-full" style={{ background: 'var(--hi)' }} />
              )}
              <span className="font-mono text-[9px] text-muted-foreground">{String(i + 1).padStart(2, '0')}</span>
              <span className={cn('flex items-center gap-2', tab === item.id && 'hl rounded-sm')}>
                {icons[item.id]}
                {item.label}
              </span>
            </button>
          ))}
        </nav>

        <div className="flex-1" />

        <div className="mt-2 flex items-center gap-2.5 px-2 pt-3" style={{ borderTop: '1px solid var(--line)' }}>
          <div className="flex size-7 items-center justify-center rounded-full border border-border bg-secondary font-mono text-[10px] text-muted-foreground">
            {t('appShell.title').slice(0, 1).toUpperCase()}
          </div>
          <span className="truncate text-[12.5px] text-muted-foreground">{t('appShell.title')}</span>
          <span className="ml-auto text-[9px] text-muted-foreground">zh-CN</span>
        </div>
      </aside>

      {/* Main column */}
      <div className="flex min-w-0 flex-1 flex-col">
        {/* Topbar */}
        <header className="flex items-center gap-3 border-b px-5 py-2.5" style={{ borderColor: 'var(--line)' }}>
          {/* Mobile brand + tab strip */}
          <div className="flex w-full flex-col gap-1.5 md:hidden">
            <div className="flex items-center gap-2">
              <div className="serif text-base font-semibold">{t('appShell.title')}</div>
              <div className="ml-auto flex items-center gap-2">
                <div className="pill flex items-center gap-1.5 font-mono text-[9.5px] text-muted-foreground">
                  <span
                    className={cn(
                      'inline-block size-1.5 rounded-full',
                      healthy === null ? 'bg-warn' : healthy ? 'bg-ok shadow-[0_0_5px_var(--ok)]' : 'bg-destructive',
                    )}
                  />
                  {healthy === null ? t('appShell.checking') : healthy ? t('appShell.healthy') : t('appShell.degraded')}
                </div>
                <ThemeToggle />
              </div>
            </div>
            <div className="flex gap-1 overflow-x-auto">
              {tabs.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => setTab(item.id)}
                  className={cn(
                    'whitespace-nowrap rounded px-2 py-1 text-xs font-medium',
                    tab === item.id ? 'hl' : 'text-muted-foreground',
                  )}
                >
                  {item.label}
                </button>
              ))}
            </div>
          </div>

          {/* Desktop: breadcrumb-ish + status + theme */}
          <div className="hidden items-center gap-3 md:flex">
            <div className="font-mono text-[10.5px] text-muted-foreground">
              {t('appShell.crumb', { section: t(`appShell.tabs.${tab}`) })}
            </div>
            <div className="pill flex items-center gap-1.5 rounded-full border px-2.5 py-1 font-mono text-[9.5px] text-muted-foreground" style={{ borderColor: 'var(--line)' }}>
              <span
                className={cn(
                  'inline-block size-1.5 rounded-full',
                  healthy === null ? 'bg-warn' : healthy ? 'bg-ok shadow-[0_0_5px_var(--ok)]' : 'bg-destructive',
                )}
              />
              ENGINE {healthy === null ? t('appShell.checking') : healthy ? 'OK · 0 QUEUED' : t('appShell.degraded')}
            </div>
          </div>

          <div className="ml-auto hidden items-center gap-2 md:flex">
            <ThemeToggle />
          </div>
        </header>

        {/* Content — keyed so tab switches animate in */}
        <main key={tab} className="animate-in fade-in slide-in-from-bottom-1 duration-200 flex-1 p-5 md:p-6">
          {page}
        </main>
      </div>
    </div>
  )
}
