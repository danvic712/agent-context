import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { BackToTop } from './back-to-top'

interface PageHeaderProps {
  eyebrow: string
  title: string
  description?: string
  actions?: ReactNode
  className?: string
}

export function PageHeader({ eyebrow, title, description, actions, className }: PageHeaderProps) {
  return (
    <header className={cn('ui-page-header', className)}>
      <div className="ui-page-header__copy">
        <p className="ui-page-header__eyebrow">{eyebrow}</p>
        <h1 className="ui-page-header__title">{title}</h1>
        {description && <p className="ui-page-header__description">{description}</p>}
      </div>
      {actions && <div className="ui-page-header__actions">{actions}</div>}
    </header>
  )
}

interface PageFrameProps {
  children: ReactNode
  header?: ReactNode
  index?: ReactNode
  indexClassName?: string
  className?: string
}

export function PageFrame({ children, header, index, indexClassName, className }: PageFrameProps) {
  return (
    <div className={cn('ui-page', className)} data-ui-page>
      {header}
      <div className={cn('ui-page-layout', index && 'ui-page-layout--indexed')}>
        {index && <aside className={cn('ui-page-index', indexClassName)}>{index}</aside>}
        <div className="ui-page-content">{children}</div>
      </div>
      <BackToTop />
    </div>
  )
}
