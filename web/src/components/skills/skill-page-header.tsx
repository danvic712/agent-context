import { useTranslation } from 'react-i18next'
import { UploadIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { PageHeader } from '@/components/ui/page-frame'
import type { SkillItem } from '@/lib/api'

interface SkillPageHeaderProps {
  items: SkillItem[]
  loading: boolean
  hasMore: boolean
  onUpload: () => void
}

function SnapshotStat({
  value,
  label,
  detail,
}: {
  value: string
  label: string
  detail: string
}) {
  return (
    <div className="skill-library-snapshot__stat">
      <strong>{value}</strong>
      <span>{label}</span>
      <small>{detail}</small>
    </div>
  )
}

export function SkillPageHeader({ items, loading, hasMore, onUpload }: SkillPageHeaderProps) {
  const { t, i18n } = useTranslation()
  const latestUpdatedAt = items.reduce<string | null>((latest, item) => {
    if (!latest || new Date(item.updatedAtUtc).getTime() > new Date(latest).getTime()) return item.updatedAtUtc
    return latest
  }, null)
  const domainCount = new Set(items.map((item) => item.domainName.trim()).filter(Boolean)).size
  const updatedLabel = latestUpdatedAt
    ? new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(latestUpdatedAt))
    : t('skills.snapshotNoDate')

  return (
    <>
      <PageHeader
        eyebrow={t('skills.libraryKicker')}
        title={t('skills.libraryTitle')}
        description={t('skills.libraryDescription')}
        actions={(
          <div className="skill-library-header-actions">
            <Badge variant="accent" className="font-mono text-[10px]">{t('skills.localOnly')}</Badge>
            <Button type="button" size="sm" onClick={onUpload}>
              <UploadIcon data-icon="inline-start" className="size-3.5" />
              {t('skills.uploadSkill')}
            </Button>
          </div>
        )}
      />

      <section
        className="skill-library-snapshot"
        aria-busy={loading}
        aria-labelledby="skill-library-snapshot-title"
      >
        <div className="skill-library-snapshot__intro">
          <p className="kicker">{t('skills.snapshotKicker')}</p>
          <h2 id="skill-library-snapshot-title">{t('skills.snapshotTitle')}</h2>
          <p>{t('skills.snapshotDescription')}</p>
        </div>
        <SnapshotStat
          value={loading ? '—' : String(items.length)}
          label={t('skills.snapshotVisible')}
          detail={hasMore ? t('skills.snapshotVisibleDetailMore') : t('skills.snapshotVisibleDetail')}
        />
        <SnapshotStat
          value={loading ? '—' : String(domainCount)}
          label={t('skills.snapshotDomains')}
          detail={t('skills.snapshotDomainsDetail')}
        />
        <SnapshotStat
          value={loading ? '—' : updatedLabel}
          label={t('skills.snapshotUpdated')}
          detail={t('skills.snapshotUpdatedDetail')}
        />
      </section>
    </>
  )
}
