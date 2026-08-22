import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BookOpenIcon, ExternalLinkIcon, SettingsIcon, SparklesIcon, WrenchIcon } from 'lucide-react'
import { Navigate, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { ThemeToggle } from '@/components/theme-toggle'
import { KnowledgeManager } from '@/components/knowledge-manager'
import { SettingsPage } from '@/components/settings-page'
import { SkillDetailPage } from '@/components/skills/skill-detail-page'
import { SkillsLibraryPage } from '@/components/skills/skills-library-page'
import { SkillUploadPage } from '@/components/skills/skill-upload-page'
import { appTabs, getTabFromPath, type AppTab } from '@/lib/app-routes'
import { getDashboardUrl, getEngineHealth, type EngineHealth } from '@/lib/api'
import { getEngineHealthState } from '@/lib/engine-health-state'
import { cn } from '@/lib/utils'

const icons: Record<AppTab, React.ReactNode> = {
  knowledge: <BookOpenIcon className="size-3.5" />,
  skills: <WrenchIcon className="size-3.5" />,
  settings: <SettingsIcon className="size-3.5" />,
}

export function AppShell() {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const tab = getTabFromPath(pathname)
  const [engineHealth, setEngineHealth] = useState<EngineHealth | null>(null)
  const [engineHealthError, setEngineHealthError] = useState(false)
  const [dashboardUrl, setDashboardUrl] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    getEngineHealth()
      .then((health) => {
        if (!cancelled) {
          setEngineHealth(health)
          setEngineHealthError(false)
        }
      })
      .catch(() => {
        if (!cancelled) setEngineHealthError(true)
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

  const tabs = appTabs.map((item) => ({ ...item, label: t(`appShell.tabs.${item.id}`) }))
  const engineState = getEngineHealthState(engineHealth, engineHealthError)
  const engineLabel = engineState === 'loading'
    ? t('appShell.checking')
    : engineState === 'healthy'
      ? t('appShell.engineHealthy', { queued: engineHealth?.queuedSessions ?? 0 })
      : engineState === 'attention'
        ? t('appShell.engineAttention')
        : t('appShell.engineDegraded')

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
              className="flex size-7 items-center justify-center rounded-[13px_17px_13px_17px] text-white"
              style={{ background: 'linear-gradient(145deg, var(--accent), color-mix(in srgb, var(--accent) 70%, var(--hi)))', boxShadow: '0 4px 12px var(--accent-shadow)', border: '1px solid var(--line2)', transform: 'rotate(-4deg)' }}
            >
              <SparklesIcon className="size-4" />
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
              <NavLink
                key={item.id}
                to={item.path}
                end={item.id !== 'skills'}
                className={({ isActive }) => cn(
                  'flex shrink-0 items-center gap-1.5 whitespace-nowrap rounded-full px-3 py-1.5 text-[12.5px] font-medium transition-all duration-150',
                  isActive ? 'text-primary-foreground' : 'text-muted-foreground hover:bg-[var(--hover)] hover:text-foreground',
                )}
                style={({ isActive }) => isActive ? { background: 'var(--accent)', boxShadow: '0 3px 10px var(--accent-shadow)' } : undefined}
              >
                {icons[item.id]}
                {item.label}
              </NavLink>
            ))}
          </nav>

          {/* Status + dashboard + theme */}
          <div className="flex shrink-0 items-center gap-2">
            {dashboardUrl && (
              <button
                type="button"
                // Single-port model: when DASHBOARD_URL points at the portal's
                // own prefix (same origin, e.g. /monitor), navigate in-place;
                // an external dashboard URL opens in a new window instead.
                onClick={() => {
                  const dash = new URL(dashboardUrl, window.location.href)
                  const inPlace = dash.origin === window.location.origin && dash.pathname !== '/'
                  if (inPlace) {
                    // Preserve any dashboard query/hash state while keeping the
                    // navigation on the portal's same-origin surface.
                    window.location.href = `${dash.pathname}${dash.search}${dash.hash}`
                  } else {
                    window.open(dash.href, '_blank', 'noopener,noreferrer')
                  }
                }}
                title={dashboardUrl}
                className="flex shrink-0 items-center gap-1.5 rounded-full border px-2.5 py-1.5 text-[11.5px] font-medium text-muted-foreground transition-all duration-150 hover:text-foreground"
                style={{ borderColor: 'var(--line)', background: 'var(--card2-paper)' }}
              >
                <ExternalLinkIcon className="size-3.5" />
                {t('appShell.dashboard')}
              </button>
            )}
            <NavLink
              to="/settings#engine-health"
              aria-label={t('appShell.engineStatusLabel')}
              className="hidden items-center gap-1.5 rounded-full border px-2.5 py-1 font-mono text-[9.5px] text-muted-foreground transition-colors hover:text-foreground lg:flex"
              style={{ borderColor: 'var(--line)', background: 'var(--card2-paper)' }}
            >
              <span
                className={cn(
                  'inline-block size-1.5 rounded-full',
                  engineState === 'loading' ? 'bg-warn' : engineState === 'healthy' ? 'bg-ok shadow-[0_0_5px_var(--ok)]' : engineState === 'attention' ? 'bg-warn' : 'bg-destructive',
                )}
              />
              {engineLabel}
            </NavLink>
            <ThemeToggle />
          </div>
        </div>
      </header>

      {/* Content — keyed so tab switches animate in */}
      <main key={tab} className="animate-in fade-in slide-in-from-bottom-1 duration-200 flex-1 p-4 md:p-6">
        <Routes>
          <Route path="/knowledge" element={<KnowledgeManager />} />
          <Route path="/skills" element={<SkillsLibraryPage />} />
          <Route path="/skills/upload" element={<SkillUploadPage />} />
          <Route path="/skills/view/:id" element={<SkillDetailPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="*" element={<Navigate to="/knowledge" replace />} />
        </Routes>
      </main>
    </div>
  )
}
