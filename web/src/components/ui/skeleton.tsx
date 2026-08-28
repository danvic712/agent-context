import { cn } from '@/lib/utils'

/**
 * A presentational placeholder used while content loads. The containing loading
 * region owns aria-busy and the announcement; individual blocks stay silent.
 */
function Skeleton({ className, ...props }: React.ComponentProps<'div'>) {
  return (
    <div
      data-slot="skeleton"
      aria-hidden="true"
      className={cn('animate-pulse rounded-md bg-muted/60', className)}
      {...props}
    />
  )
}

export { Skeleton }
