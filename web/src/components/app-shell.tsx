import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  BookOpenIcon,
  ChevronRightIcon,
  PanelLeftCloseIcon,
  PanelLeftOpenIcon,
  SettingsIcon,
  SparklesIcon,
  WrenchIcon,
} from 'lucide-react'
import { NavLink, Route, Routes, useLocation } from 'react-router-dom'
import { ThemeToggle } from '@/components/theme-toggle'
import { ActionFeedbackProvider } from '@/components/ui/action-feedback'
import { LanguageSwitcher } from '@/components/ui/language-switcher'
import { ErrorPage, NotFoundPage } from '@/components/error-pages/error-page'
import { KnowledgeManager } from '@/components/knowledge-manager'
import { SettingsPage } from '@/components/settings/settings-page'
import { SkillsLibraryPage } from '@/components/skills/skills-library-page'
import { SkillUploadPage } from '@/components/skills/skill-upload-page'
import { appTabs, errorPath, getTabFromPath, type AppTab } from '@/lib/app-routes'
import { getEngineHealth, type EngineHealth } from '@/lib/api'
import { getEngineHealthState } from '@/lib/engine-health-state'
import { cn } from '@/lib/utils'

const icons: Record<AppTab, React.ReactNode> = {
  knowledge: <BookOpenIcon className="size-3.5" />,
  skills: <WrenchIcon className="size-3.5" />,
  settings: <SettingsIcon className="size-3.5" />,
}

function getWorkspaceMark(name: string): string {
  const words = name.trim().split(/\s+/).filter(Boolean)
  if (words.length > 1) return words.slice(0, 2).map((word) => word[0]).join('').toUpperCase()
  return Array.from(name.trim().replace(/\s/g, '')).slice(0, 2).join('').toUpperCase() || 'AC'
}

interface AppShellProps {
  workspaceName?: string | null
}

export function AppShell({ workspaceName }: AppShellProps) {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const normalizedPath = pathname.replace(/\/+$/, '') || '/'
  const tab = getTabFromPath(pathname)
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false)
  const [mobileNavOpen, setMobileNavOpen] = useState(false)
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

  useEffect(() => {
    setMobileNavOpen(false)
  }, [pathname])

  const tabs = appTabs.map((item) => ({ ...item, label: t(`appShell.tabs.${item.id}`) }))
  const displayedWorkspaceName = workspaceName?.trim() || t('appShell.workspace')
  const workspaceMark = getWorkspaceMark(displayedWorkspaceName)
  const engineState = getEngineHealthState(engineHealth, engineHealthError)
  const engineLabel = engineState === 'loading'
    ? t('appShell.checking')
    : engineState === 'healthy'
      ? t('appShell.engineHealthy', { queued: engineHealth?.queuedSessions ?? 0 })
      : engineState === 'attention'
        ? t('appShell.engineAttention')
        : t('appShell.engineDegraded')
  const pageHeading = normalizedPath === '/knowledge'
    ? t('knowledge.libraryTitle')
    : normalizedPath === '/skills/upload'
      ? t('skills.uploadTitle')
      : normalizedPath.startsWith('/skills')
        ? t('skills.libraryTitle')
        : normalizedPath === '/settings'
          ? t('settings.pageTitle')
          : null

  return (
    <ActionFeedbackProvider>
      <div
        className="ui-shell"
        data-sidebar-collapsed={sidebarCollapsed}
        data-mobile-nav-open={mobileNavOpen}
      >
        <header className="ui-shell__header">
          <div className="ui-shell__inner">
            <div className="ui-shell__brand-row">
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
              <button
                type="button"
                className="ui-shell__collapse-toggle"
                aria-label={sidebarCollapsed ? t('appShell.expandMenu') : t('appShell.collapseMenu')}
                title={sidebarCollapsed ? t('appShell.expandMenu') : t('appShell.collapseMenu')}
                onClick={() => setSidebarCollapsed((current) => !current)}
              >
                {sidebarCollapsed
                  ? <PanelLeftOpenIcon aria-hidden="true" />
                  : <PanelLeftCloseIcon aria-hidden="true" />}
              </button>
              <button
                type="button"
                className="ui-shell__mobile-toggle"
                aria-label={mobileNavOpen ? t('appShell.closeMenu') : t('appShell.menu')}
                aria-expanded={mobileNavOpen}
                aria-controls="app-primary-navigation"
                onClick={() => setMobileNavOpen((current) => !current)}
              >
                {mobileNavOpen
                  ? <PanelLeftCloseIcon aria-hidden="true" />
                  : <PanelLeftOpenIcon aria-hidden="true" />}
                <span>{mobileNavOpen ? t('appShell.closeMenu') : t('appShell.menu')}</span>
              </button>
            </div>

            <div className="ui-shell__workspace" aria-label={displayedWorkspaceName} title={displayedWorkspaceName}>
              <span className="ui-shell__workspace-mark" aria-hidden="true">{workspaceMark}</span>
              <span className="ui-shell__workspace-copy">
                <span className="ui-shell__workspace-label">{displayedWorkspaceName}</span>
                <span className="ui-shell__workspace-hint">{t('appShell.workspaceHint')}</span>
              </span>
              <ChevronRightIcon className="ui-shell__workspace-chevron" aria-hidden="true" />
            </div>

            <nav id="app-primary-navigation" className="ui-shell__nav" aria-label={t('appShell.primaryNavigation')}>
              <div className="ui-shell__nav-label">{t('appShell.librarySection')}</div>
              {tabs.map((item) => (
                <NavLink
                  key={item.id}
                  to={item.path}
                  end={item.id !== 'skills'}
                  title={sidebarCollapsed ? item.label : undefined}
                  className={({ isActive }) => cn('ui-shell__nav-link', isActive && 'ui-shell__nav-link--active')}
                  onClick={() => setMobileNavOpen(false)}
                >
                  <span className="ui-shell__nav-icon" aria-hidden="true">{icons[item.id]}</span>
                  <span className="ui-shell__nav-label-text">{item.label}</span>
                  <ChevronRightIcon className="ui-shell__nav-chevron" aria-hidden="true" />
                </NavLink>
              ))}
            </nav>

          </div>
        </header>

        {/* Content — keyed so tab switches animate in */}
        <main key={tab} className="ui-shell__main animate-in fade-in slide-in-from-bottom-1 duration-200">
          <div className="ui-shell__topbar">
            <div className={cn('ui-shell__topbar-inner', !pageHeading && 'ui-shell__topbar-inner--tools-only')}>
              {pageHeading && (
                <div className="ui-shell__page-heading">
                  <span className="ui-shell__page-heading-mark" aria-hidden="true" />
                  <h1 className="ui-shell__page-heading-title">{pageHeading}</h1>
                </div>
              )}
              <div className="ui-shell__topbar-side">
                <div className="ui-shell__main-tools" role="group" aria-label={t('appShell.systemSection')}>
                  <div
                    role="status"
                    aria-label={`${t('appShell.tabs.health')}: ${engineLabel}`}
                    title={engineLabel}
                    className="ui-shell__engine-status ui-shell__main-engine-status"
                  >
                    <span
                      className={cn(
                        'ui-shell__engine-dot',
                        engineState === 'loading' ? 'ui-shell__engine-dot--warn' : engineState === 'healthy' ? 'ui-shell__engine-dot--ok' : engineState === 'attention' ? 'ui-shell__engine-dot--warn' : 'ui-shell__engine-dot--error',
                      )}
                    />
                    <span className="ui-shell__engine-copy">
                      <span className="ui-shell__engine-name">{t('appShell.tabs.health')}</span>
                      <span className="ui-shell__engine-label">{engineLabel}</span>
                    </span>
                  </div>
                  <div className="ui-shell__main-preferences">
                    <LanguageSwitcher />
                    <ThemeToggle />
                  </div>
                </div>
              </div>
            </div>
          </div>
          <Routes>
            <Route path="/knowledge" element={<KnowledgeManager />} />
            <Route path="/skills" element={<SkillsLibraryPage />} />
            <Route path="/skills/upload" element={<SkillUploadPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path={errorPath} element={<ErrorPage />} />
            <Route path="*" element={<NotFoundPage />} />
          </Routes>
        </main>
      </div>
    </ActionFeedbackProvider>
  )
}
