import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/utils'

interface ActionBarProps extends HTMLAttributes<HTMLDivElement> {
  status?: ReactNode
  children: ReactNode
  sticky?: boolean
}

export function ActionBar({ status, children, sticky = false, className, ...props }: ActionBarProps) {
  return (
    <div className={cn('ui-action-bar', sticky && 'ui-action-bar--sticky', className)} {...props}>
      {status && <div className="ui-action-bar__status">{status}</div>}
      <div className="ui-action-bar__actions">{children}</div>
    </div>
  )
}

export function ActionBarStatus({ children }: { children: ReactNode }) {
  return (
    <>
      <span className="ui-action-bar__status-dot" aria-hidden="true" />
      {children}
    </>
  )
}
