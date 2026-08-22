import { ArrowLeftIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { SkillPageHeader } from './skill-page-header'
import { SkillUploadForm } from './skill-upload-form'
import { uploadSkill, type SkillUploadInput } from '@/lib/api'

export function SkillUploadPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  const submit = async (input: SkillUploadInput, reportProgress: (progress: number) => void) => {
    const created = await uploadSkill(input, reportProgress)
    navigate('/skills', { state: { highlightId: created.id, successSlug: created.slug } })
  }

  return (
    <div className="mx-auto max-w-4xl">
      <SkillPageHeader
        eyebrow={t('skills.uploadKicker')}
        title={t('skills.uploadTitle')}
        description={t('skills.uploadDescription')}
        actions={(
          <Link to="/skills" className="inline-flex h-7 items-center gap-1.5 rounded-lg px-2.5 text-[0.8rem] font-medium text-muted-foreground transition hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50">
            <ArrowLeftIcon className="size-3.5" />{t('skills.backToLibrary')}
          </Link>
        )}
      />
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_240px]">
        <div className="rounded-2xl border border-border/80 bg-card p-5 shadow-sm md:p-7">
          <SkillUploadForm onSubmit={submit} />
        </div>
        <aside className="h-fit rounded-2xl border border-border/80 bg-card/70 p-5 shadow-sm">
          <p className="kicker">{t('skills.uploadChecklistTitle')}</p>
          <ul className="mt-4 space-y-3 text-xs leading-5 text-muted-foreground">
            <li className="flex gap-2"><span className="text-ok">✓</span>{t('skills.uploadChecklistRoot')}</li>
            <li className="flex gap-2"><span className="text-ok">✓</span>{t('skills.uploadChecklistSafe')}</li>
            <li className="flex gap-2"><span className="text-ok">✓</span>{t('skills.uploadChecklistBinary')}</li>
          </ul>
        </aside>
      </div>
    </div>
  )
}
