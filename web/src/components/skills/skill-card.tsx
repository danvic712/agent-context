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
  const domain = item.domainName || t('skills.noDomain')

  return (
    <Link
      to={`/skills/view/${item.id}`}
      aria-label={t('skills.viewPackageFor', { name: item.name })}
      className="block rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
    >
      <Card
        className={cn(
          'skill-library-card group p-0',
          highlighted && 'ring-2 ring-[var(--hi)] ring-offset-2 ring-offset-background',
        )}
        data-highlighted={highlighted || undefined}
      >
        <CardContent className="skill-library-card__content p-0">
          <div className="skill-library-card__topline">
            <span className="skill-library-card__domain">{domain}</span>
            <span className="skill-library-card__source">
              <span className="skill-library-card__source-dot" aria-hidden="true" />
              {t(skillSourceKey(item.sourceType))}
            </span>
          </div>

          <div className="skill-library-card__main">
            <div className="skill-library-card__icon" aria-hidden="true">
              <BookOpenIcon className="size-5" />
            </div>
            <div className="skill-library-card__heading">
              <h3>{item.name}</h3>
              <p className="skill-library-card__path">{domain} / {item.slug}</p>
            </div>
            <Badge variant="outline" className="skill-library-card__version font-mono text-[10px]">
              {t('skills.version', { version: item.version })}
            </Badge>
          </div>

          <p className="skill-library-card__description">
            {item.description || t('skills.noDescription')}
          </p>

          <div className="skill-library-card__footer">
            <span className="skill-library-card__updated">{t('skills.updatedOn', { date: updatedAt })}</span>
            <span className="skill-library-card__action">
              {t('skills.viewSkill')}
              <ArrowUpRightIcon className="size-3.5 transition-transform group-hover:-translate-y-0.5 group-hover:translate-x-0.5" />
            </span>
          </div>
        </CardContent>
      </Card>
    </Link>
  )
}
