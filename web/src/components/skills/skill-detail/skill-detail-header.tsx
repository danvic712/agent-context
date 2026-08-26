import { ArrowLeftIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { PageHeader } from '@/components/ui/page-frame'

interface SkillDetailHeaderProps {
  onBack: () => void
}

export function SkillDetailHeader({ onBack }: SkillDetailHeaderProps) {
  const { t } = useTranslation()

  return (
    <PageHeader
      className="skill-detail-header"
      eyebrow={t('skills.detailKicker')}
      title={t('skills.detailPageTitle')}
      actions={(
        <Link to="/skills" className="ui-inline-action" onClick={onBack}>
          <ArrowLeftIcon className="size-3.5" />
          {t('skills.backToLibrary')}
        </Link>
      )}
    />
  )
}
