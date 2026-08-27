import { ArrowUpRightIcon, BookOpenIcon, LoaderCircleIcon, Trash2Icon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent } from '@/components/ui/card'
import type { SkillItem } from '@/lib/api'
import { cn } from '@/lib/utils'
import { skillSourceKey } from './skill-source'

interface SkillCardProps {
  item: SkillItem
  highlighted?: boolean
  deleting?: boolean
  onDelete: () => void
}

export function SkillCard({ item, highlighted = false, deleting = false, onDelete }: SkillCardProps) {
  const { t, i18n } = useTranslation()
  const updatedAt = new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(item.updatedAtUtc))
  const domain = item.domainName || t('skills.noDomain')

  return (
    <div className="skill-library-card-shell">
      <Link
        to={`/skills/view/${item.id}`}
        aria-label={t('skills.viewPackageFor', { name: item.name })}
        className="block h-full rounded-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
      >
        <Card
          className={cn(
            'skill-library-card group h-full p-0',
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
      <Button
        type="button"
        variant="destructive"
        size="icon-sm"
        className="skill-library-card__delete"
        aria-label={t('skills.deleteAria', { slug: item.slug })}
        title={t('skills.deleteAria', { slug: item.slug })}
        disabled={deleting}
        onClick={(event) => {
          event.preventDefault()
          event.stopPropagation()
          onDelete()
        }}
      >
        {deleting
          ? <LoaderCircleIcon className="size-3.5 animate-spin" />
          : <Trash2Icon className="size-3.5" />}
      </Button>
    </div>
  )
}
