import type { ReactNode } from 'react'

interface SkillPageHeaderProps {
  eyebrow: string
  title: string
  description: string
  actions?: ReactNode
}

export function SkillPageHeader({ eyebrow, title, description, actions }: SkillPageHeaderProps) {
  return (
    <div className="mb-5 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
      <div className="min-w-0">
        <p className="kicker mb-2">{eyebrow}</p>
        <h1 className="serif text-3xl font-semibold tracking-tight text-foreground md:text-4xl">{title}</h1>
        <p className="mt-2 max-w-2xl text-sm leading-6 text-muted-foreground">{description}</p>
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}
