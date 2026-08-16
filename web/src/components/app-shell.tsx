import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  ActivityIcon,
  BarChart3Icon,
  BookOpenIcon,
  DatabaseIcon,
  FolderArchiveIcon,
  FolderSearchIcon,
  SettingsIcon,
  WrenchIcon,
} from 'lucide-react'
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
      {/* Sidebar (Direction D) */}
      <aside className="hidden w-[232px] shrink-0 flex-col border-r border-border bg-sidebar px-3 py-4 md:flex">
        <div className="flex items-center gap-2.5 px-2 pb-4 text-sm font-semibold tracking-tight">
          <div className="flex size-[22px] items-center justify-center rounded-md bg-gradient-to-br from-[#5e6ad2] to-[#8b5cf6] text-[11px] font-bold text-white">
            AC
          </div>
          <DatabaseIcon className="size-4 text-muted-foreground" />
          {t('appShell.title')}
        </div>

        <nav className="flex flex-col gap-0.5">
          {tabs.map((item) => (
            <button
              key={item.id}
              type="button"
              onClick={() => setTab(item.id)}
              aria-current={tab === item.id ? 'page' : undefined}
              className={cn(
                'flex items-center gap-2.5 rounded-lg px-2.5 py-2 text-[13px] font-medium text-muted-foreground transition-colors',
                tab === item.id && 'bg-accent/15 text-foreground [&>svg]:text-[#5e6ad2]',
                tab !== item.id && 'hover:bg-secondary hover:text-foreground',
              )}
            >
              {icons[item.id]}
              {item.label}
            </button>
          ))}
        </nav>

        <div className="flex-1" />

        <div className="mt-2 flex items-center gap-2.5 border-t border-border px-2 pt-3 text-[12.5px]">
          <div className="flex size-7 items-center justify-center rounded-full border border-border bg-secondary text-[11px] text-muted-foreground">
            {t('appShell.title').slice(0, 1).toUpperCase()}
          </div>
          <span className="truncate text-muted-foreground">{t('appShell.title')}</span>
        </div>
      </aside>

      {/* Main column */}
      <div className="flex min-w-0 flex-1 flex-col">
        {/* Topbar */}
        <header className="flex items-center gap-3 border-b border-border px-5 py-3">
          {/* Mobile brand */}
          <div className="flex items-center gap-2 font-semibold md:hidden">
            <DatabaseIcon className="size-4 text-muted-foreground" />
            {t('appShell.title')}
          </div>

          <div className="flex items-center gap-2 md:hidden">
            {tabs.map((item) => (
              <button
                key={item.id}
                type="button"
                onClick={() => setTab(item.id)}
                className={cn(
                  'rounded-md px-2 py-1 text-xs font-medium',
                  tab === item.id ? 'bg-accent/15 text-foreground' : 'text-muted-foreground',
                )}
              >
                {item.label}
              </button>
            ))}
          </div>

          {/* Engine status pill */}
          <div className="hidden items-center gap-2 rounded-lg border border-border bg-card px-3 py-1.5 text-[12.5px] text-muted-foreground md:flex">
            <span
              className={cn(
                'size-[7px] rounded-full',
                healthy === null ? 'bg-warn' : healthy ? 'bg-[#22a06b] shadow-[0_0_0_3px_rgba(34,160,107,0.18)]' : 'bg-destructive',
              )}
            />
            {healthy === null
              ? t('appShell.checking')
              : healthy
                ? t('appShell.healthy')
                : t('appShell.degraded')}
          </div>

          <div className="ml-auto flex items-center gap-2.5">
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
