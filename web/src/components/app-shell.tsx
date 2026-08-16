import { useEffect, useState } from 'react'
import { CircleCheckIcon, DatabaseIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { AnalyticsOverview } from '@/components/analytics-overview'
import { KnowledgeManager } from '@/components/knowledge-manager'
import { SkillManager } from '@/components/skill-manager'
import { getHealth } from '@/lib/api'

type Tab = 'knowledge' | 'review' | 'skills' | 'analytics'

export function AppShell() {
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

  return (
    <div className="flex min-h-svh flex-col">
      <header className="flex items-center justify-between border-b px-6 py-3">
        <div className="flex items-center gap-2 font-semibold">
          <DatabaseIcon data-icon="inline-start" />
          Agent Context
        </div>
        {healthy === null ? (
          <Badge variant="secondary">checking…</Badge>
        ) : healthy ? (
          <Badge variant="default">
            <CircleCheckIcon data-icon="inline-start" />
            healthy
          </Badge>
        ) : (
          <Badge variant="destructive">degraded</Badge>
        )}
      </header>
      <main className="flex flex-1 flex-col gap-6 p-6">
        <nav className="flex items-center gap-2">
          <Button
            variant={tab === 'knowledge' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTab('knowledge')}
          >
            Knowledge
          </Button>
          <Button
            variant={tab === 'review' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTab('review')}
          >
            Review
          </Button>
          <Button
            variant={tab === 'skills' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTab('skills')}
          >
            Skills
          </Button>
          <Button
            variant={tab === 'analytics' ? 'default' : 'outline'}
            size="sm"
            onClick={() => setTab('analytics')}
          >
            Analytics
          </Button>
        </nav>
        {tab === 'skills' ? (
          <SkillManager />
        ) : tab === 'analytics' ? (
          <AnalyticsOverview />
        ) : (
          <KnowledgeManager mode={tab === 'review' ? 'review' : 'all'} />
        )}
      </main>
    </div>
  )
}
