import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BookOpenIcon, SettingsIcon, SparklesIcon, WrenchIcon } from 'lucide-react'
import { Navigate, NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { ThemeToggle } from '@/components/theme-toggle'
import { ActionFeedbackProvider } from '@/components/ui/action-feedback'
import { LanguageSwitcher } from '@/components/ui/language-switcher'
import { KnowledgeManager } from '@/components/knowledge-manager'
import { SettingsPage } from '@/components/settings-page'
import { SkillDetailPage } from '@/components/skills/skill-detail-page'
import { SkillsLibraryPage } from '@/components/skills/skills-library-page'
import { SkillUploadPage } from '@/components/skills/skill-upload-page'
import { appTabs, getTabFromPath, type AppTab } from '@/lib/app-routes'
import { getEngineHealth, type EngineHealth } from '@/lib/api'
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
    <ActionFeedbackProvider>
      <div className="ui-shell">
        <header className="ui-shell__header">
          <div className="ui-shell__inner">
            {/* Brand */}
            <NavLink
              to="/knowledge"
              className="ui-shell__brand"
              aria-label={t('appShell.home')}
            >
              <div className="ui-shell__mark" aria-hidden="true">
                <SparklesIcon className="size-4" />
              </div>
              <div className="ui-shell__brand-copy">
                <div className="ui-shell__brand-name">{t('appShell.title')}</div>
                <div className="ui-shell__brand-sub">{t('appShell.brandTagline')}</div>
              </div>
            </NavLink>

            {/* Nav pills */}
            <nav className="ui-shell__nav" aria-label={t('appShell.primaryNavigation')}>
              {tabs.map((item) => (
                <NavLink
                  key={item.id}
                  to={item.path}
                  end={item.id !== 'skills'}
                  className={({ isActive }) => cn('ui-shell__nav-link', isActive && 'ui-shell__nav-link--active')}
                >
                  {icons[item.id]}
                  {item.label}
                </NavLink>
              ))}
            </nav>

            {/* Status + theme */}
            <div className="ui-shell__utilities">
              <NavLink
                to="/settings#engine-health"
                aria-label={t('appShell.engineStatusLabel')}
                className="ui-shell__engine-status"
              >
                <span
                  className={cn(
                    'inline-block size-1.5 rounded-full',
                    engineState === 'loading' ? 'bg-warn' : engineState === 'healthy' ? 'bg-ok shadow-[0_0_5px_var(--ok)]' : engineState === 'attention' ? 'bg-warn' : 'bg-destructive',
                  )}
                />
                {engineLabel}
              </NavLink>
              <LanguageSwitcher />
              <ThemeToggle />
            </div>
          </div>
        </header>

        {/* Content — keyed so tab switches animate in */}
        <main key={tab} className="ui-shell__main animate-in fade-in slide-in-from-bottom-1 duration-200">
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
    </ActionFeedbackProvider>
  )
}
