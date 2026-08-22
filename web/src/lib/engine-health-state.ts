import type { EngineHealth } from '@/lib/api'

export type EngineHealthState = 'loading' | 'healthy' | 'attention' | 'degraded'

/**
 * Shared operational state for the global marker and the Settings panel.
 * Liveness remains a separate API contract; this state only describes the
 * Learning Engine queue and retry surface.
 */
export function getEngineHealthState(
  health: EngineHealth | null,
  hasError = false,
): EngineHealthState {
  if (!health) return hasError ? 'degraded' : 'loading'
  return health.failedSessions > 0 || health.retryScheduledSessions > 0 ? 'attention' : 'healthy'
}
