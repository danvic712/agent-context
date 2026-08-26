import { Badge } from '@/components/ui/badge'
import { useTranslation } from 'react-i18next'
import type { ReactNode } from 'react'
import type { SkillDetail } from '@/lib/api'
import { formatDate } from '@/lib/formatting'

interface SkillDetailMetadataProps {
  detail: SkillDetail
  sourceLabel: string
}

function MetadataItem({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="skill-detail-metadata__item">
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  )
}

export function SkillDetailMetadata({ detail, sourceLabel }: SkillDetailMetadataProps) {
  const { t } = useTranslation()

  return (
    <section className="skill-detail-rail-card ui-surface" aria-label={t('skills.packageContext')}>
      <header className="skill-detail-rail-card__header">
        <p className="skill-detail-rail-card__eyebrow">{t('skills.packageContext')}</p>
        <Badge variant="accent">{t('skills.version', { version: detail.version })}</Badge>
      </header>
      <div className="skill-detail-rail-card__body">
        <div className="skill-detail-package-identity">
          <h2>{detail.name}</h2>
          <p>{detail.description || t('skills.noDescription')}</p>
        </div>
        <div className="skill-detail-summary__status">
          <Badge variant="accent">
            <span className="size-1.5 rounded-full bg-ok" aria-hidden="true" />
            {t('skills.statusInstalled')}
          </Badge>
          <Badge variant="outline">{t('skills.readOnly')}</Badge>
        </div>
        <dl className="skill-detail-metadata">
          <MetadataItem label={t('skills.domain')}>
            <span title={detail.domainName}>{detail.domainName}</span>
          </MetadataItem>
          <MetadataItem label={t('skills.slug')}>
            <code title={detail.slug}>{detail.slug}</code>
          </MetadataItem>
          <MetadataItem label={t('skills.detailSource')}>
            <span title={sourceLabel}>{sourceLabel}</span>
          </MetadataItem>
          <MetadataItem label={t('skills.detailUpdated')}>
            <span>{formatDate(detail.updatedAtUtc)}</span>
          </MetadataItem>
        </dl>
      </div>
    </section>
  )
}
