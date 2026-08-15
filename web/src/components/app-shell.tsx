import { useEffect, useState } from 'react'
import { CircleCheckIcon, DatabaseIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { getHealth } from '@/lib/api'

export function AppShell() {
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
        <Card>
          <CardHeader>
            <CardTitle>Platform is up</CardTitle>
            <CardDescription>
              Your admin account and personal workspace are ready. Sessions,
              knowledge, skills and usage views land with the next tickets (T2+).
            </CardDescription>
          </CardHeader>
          <CardContent>
            <Separator className="mb-4" />
            <p className="text-sm text-muted-foreground">
              Connect Craft Agents as a local MCP source pointing at{' '}
              <code className="rounded bg-muted px-1.5 py-0.5">--mcp-stdio</code> to
              start reporting sessions.
            </p>
          </CardContent>
        </Card>
      </main>
    </div>
  )
}
