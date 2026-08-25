import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BrowserRouter, Navigate, useLocation } from 'react-router-dom'
import { AppShell } from './components/app-shell'
import { FirstRunWizard } from './components/setup/first-run-wizard'
import { setupPath } from './lib/app-routes'
import { getLanguage, getSetupStatus } from './lib/api'
import i18n from './i18n'

type Phase = 'loading' | 'setup' | 'app'

export default function App() {
  return (
    <BrowserRouter>
      <AppContent />
    </BrowserRouter>
  )
}

function AppContent() {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const [phase, setPhase] = useState<Phase>('loading')

  useEffect(() => {
    // The UI language is the DB-configured platform language (T11): resolve it
    // first so the whole tree renders in it, then decide wizard vs app shell.
    getLanguage()
      .then(({ language }) => i18n.changeLanguage(language))
      .catch(() => undefined)
      .then(() => getSetupStatus())
      .then((status) => setPhase(status.configured ? 'app' : 'setup'))
      .catch(() => setPhase('setup'))
  }, [])

  const isSetupPath = pathname.replace(/\/+$/, '') === setupPath
  const content =
    phase === 'loading' ? (
      <div className="flex min-h-svh items-center justify-center text-sm text-muted-foreground">
        {t('common.loading')}
      </div>
    ) : phase === 'setup' ? (
      isSetupPath ? <FirstRunWizard onComplete={() => setPhase('app')} /> : <Navigate to={setupPath} replace />
    ) : isSetupPath ? (
      <Navigate to="/knowledge" replace />
    ) : (
      <AppShell />
    )

  return content
}
