import { ArrowUpRightIcon, BookOpenIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent } from '@/components/ui/card'
import type { SkillItem } from '@/lib/api'
import { cn } from '@/lib/utils'
import { skillSourceKey } from './skill-source'

interface SkillCardProps {
  item: SkillItem
  highlighted?: boolean
}

export function SkillCard({ item, highlighted = false }: SkillCardProps) {
  const { t, i18n } = useTranslation()
  const updatedAt = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(item.updatedAtUtc))

  return (
    <Link
      to={`/skills/view/${item.id}`}
      aria-label={t('skills.viewPackageFor', { name: item.name })}
      className="block rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
    >
      <Card
        className={cn(
          'group relative flex min-h-[188px] flex-col overflow-hidden border border-border/80 bg-card/90 shadow-sm transition duration-200 hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-lg',
          highlighted && 'ring-2 ring-[var(--hi)] ring-offset-2 ring-offset-background',
        )}
        data-highlighted={highlighted || undefined}
      >
        <CardContent className="flex flex-1 flex-col p-4">
          <div className="flex items-start gap-3">
            <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <BookOpenIcon className="size-5" />
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex items-start justify-between gap-2">
                <div className="min-w-0">
                  <h2 className="truncate text-sm font-semibold text-foreground">{item.name}</h2>
                  <p className="mt-1 truncate font-mono text-[10px] text-muted-foreground">
                    {item.domainName} / {item.slug}
                  </p>
                </div>
                <Badge variant="outline" className="shrink-0 font-mono text-[10px]">
                  {t('skills.version', { version: item.version })}
                </Badge>
              </div>
            </div>
          </div>

          <p className="mt-4 line-clamp-2 min-h-10 text-xs leading-5 text-muted-foreground">
            {item.description || t('skills.noDescription')}
          </p>

          <div className="mt-auto flex items-center justify-between gap-2 border-t border-border/70 pt-3">
            <div className="flex min-w-0 flex-wrap items-center gap-2 text-[10px] text-muted-foreground">
              <Badge variant="accent" className="font-mono text-[9px]">
                {t(skillSourceKey(item.sourceType))}
              </Badge>
              <Badge variant="outline" className="font-mono text-[9px]">
                {t('skills.statusInstalled')}
              </Badge>
              <span className="truncate">{t('skills.updatedOn', { date: updatedAt })}</span>
            </div>
            <span className="inline-flex shrink-0 items-center gap-1 rounded-md px-2 py-1.5 text-xs font-medium text-primary transition group-hover:bg-primary/10">
              {t('skills.viewSkill')}
              <ArrowUpRightIcon className="size-3.5 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5" />
            </span>
          </div>
        </CardContent>
      </Card>
    </Link>
  )
}
