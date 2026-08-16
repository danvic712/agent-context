import { cn } from '@/lib/utils'

/**
 * A pulsing placeholder used while content loads (T12): replaces bare
 * "Loading…" text across the app. Colours follow the Direction D palette via the
 * muted/card variables.
 */
function Skeleton({ className, ...props }: React.ComponentProps<'div'>) {
  return (
    <div
      data-slot="skeleton"
      aria-busy="true"
      className={cn('animate-pulse rounded-md bg-muted/60', className)}
      {...props}
    />
  )
}

export { Skeleton }
