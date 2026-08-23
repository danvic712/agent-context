import { ArrowLeftIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { SkillUploadForm } from './skill-upload-form'
import { uploadSkill, type SkillUploadInput } from '@/lib/api'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
import { Surface } from '@/components/ui/surface'

export function SkillUploadPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const submit = async (input: SkillUploadInput, reportProgress: (progress: number) => void) => {
    const created = await uploadSkill(input, reportProgress)
    navigate('/skills', { state: { highlightId: created.id, successSlug: created.slug } })
  }

  return (
    <PageFrame
      header={(
        <PageHeader
          eyebrow={t('skills.uploadKicker')}
          title={t('skills.uploadTitle')}
          description={t('skills.uploadDescription')}
          actions={(
            <Link to="/skills" className="ui-inline-action">
              <ArrowLeftIcon className="size-3.5" />{t('skills.backToLibrary')}
            </Link>
          )}
        />
      )}
    >
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_240px]">
        <Surface className="p-5 md:p-7">
          <SkillUploadForm onSubmit={submit} />
        </Surface>
        <Surface as="aside" tone="muted" className="h-fit p-5">
          <p className="kicker">{t('skills.uploadChecklistTitle')}</p>
          <ul className="mt-4 space-y-3 text-xs leading-5 text-muted-foreground">
            <li className="flex gap-2"><span className="text-ok">✓</span>{t('skills.uploadChecklistRoot')}</li>
            <li className="flex gap-2"><span className="text-ok">✓</span>{t('skills.uploadChecklistSafe')}</li>
            <li className="flex gap-2"><span className="text-ok">✓</span>{t('skills.uploadChecklistBinary')}</li>
          </ul>
        </Surface>
      </div>
    </PageFrame>
  )
}
