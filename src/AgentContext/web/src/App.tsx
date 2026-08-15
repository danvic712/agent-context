import { useEffect, useState } from 'react'
import { AppShell } from './components/app-shell'
import { FirstRunWizard } from './components/first-run-wizard'
import { getSetupStatus } from './lib/api'

type Phase = 'loading' | 'setup' | 'app'

export default function App() {
  const [phase, setPhase] = useState<Phase>('loading')

  useEffect(() => {
    getSetupStatus()
      .then((status) => setPhase(status.configured ? 'app' : 'setup'))
      .catch(() => setPhase('setup'))
  }, [])

  if (phase === 'loading') {
    return (
      <div className="flex min-h-svh items-center justify-center text-sm text-muted-foreground">
        Loading…
      </div>
    )
  }

  if (phase === 'setup') {
    return <FirstRunWizard onComplete={() => setPhase('app')} />
  }

  return <AppShell />
}
