import type { HTMLAttributes, ReactNode } from 'react'
import { cn } from '@/lib/utils'

type SurfaceElement = 'div' | 'section' | 'article' | 'aside'

interface SurfaceProps extends HTMLAttributes<HTMLElement> {
  as?: SurfaceElement
  tone?: 'default' | 'muted'
}

export function Surface({ as = 'section', tone = 'default', className, ...props }: SurfaceProps) {
  const Component = as
  return <Component className={cn('ui-surface', `ui-surface--${tone}`, className)} data-ui-surface {...props} />
}

interface SectionHeadingProps {
  eyebrow?: string
  title: string
  titleId?: string
  description?: string
  aside?: ReactNode
  className?: string
}

export function SectionHeading({ eyebrow, title, titleId, description, aside, className }: SectionHeadingProps) {
  return (
    <div className={cn('ui-section-heading', className)}>
      <div className="ui-section-heading__copy">
        {eyebrow && <p className="ui-section-heading__eyebrow">{eyebrow}</p>}
        <h2 id={titleId} className="ui-section-heading__title">{title}</h2>
        {description && <p className="ui-section-heading__description">{description}</p>}
      </div>
      {aside && <div className="ui-section-heading__aside">{aside}</div>}
    </div>
  )
}
