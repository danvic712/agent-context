import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { AppShell } from './components/app-shell'
import { FirstRunWizard } from './components/first-run-wizard'
import { getLanguage, getSetupStatus } from './lib/api'
import i18n from './i18n'

type Phase = 'loading' | 'setup' | 'app'

export default function App() {
  const { t } = useTranslation()
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

  if (phase === 'loading') {
    return (
      <div className="flex min-h-svh items-center justify-center text-sm text-muted-foreground">
        {t('common.loading')}
      </div>
    )
  }

  if (phase === 'setup') {
    return <FirstRunWizard onComplete={() => setPhase('app')} />
  }

  return <AppShell />
}
