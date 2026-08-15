export interface SetupStatus {
  configured: boolean
}

export interface SetupResult {
  userId: string
  workspaceId: string
  workspaceName: string
}

export interface HealthStatus {
  status: string
  database: string
}

async function json<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const body = (await response.json().catch(() => null)) as { message?: string } | null
    throw new Error(body?.message ?? `Request failed with status ${response.status}`)
  }
  return (await response.json()) as T
}

export async function getSetupStatus(): Promise<SetupStatus> {
  const response = await fetch('/api/setup')
  return json<SetupStatus>(response)
}

export async function postSetup(
  displayName: string,
  email: string,
  password: string,
): Promise<SetupResult> {
  const response = await fetch('/api/setup', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ displayName, email, password }),
  })
  return json<SetupResult>(response)
}

export async function getHealth(): Promise<HealthStatus> {
  const response = await fetch('/api/health')
  return json<HealthStatus>(response)
}
