import { ArrowLeftIcon, CheckIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { useState } from 'react'
import { SkillUploadForm, type SkillUploadStep } from './skill-upload-form'
import { uploadSkill, type SkillUploadInput } from '@/lib/api'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
import './skills.css'

const UPLOAD_STEPS: Array<{ id: SkillUploadStep; number: number; labelKey: string }> = [
  { id: 'package', number: 1, labelKey: 'skills.uploadStepPackage' },
  { id: 'validation', number: 2, labelKey: 'skills.uploadStepValidation' },
  { id: 'publish', number: 3, labelKey: 'skills.uploadStepPublish' },
]

export function SkillUploadPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [step, setStep] = useState<SkillUploadStep>('package')
  const [readyForValidation, setReadyForValidation] = useState(false)

  const submit = async (input: SkillUploadInput, reportProgress: (progress: number) => void) => {
    const created = await uploadSkill(input, reportProgress)
    navigate('/skills', { state: { highlightId: created.id, successSlug: created.slug } })
  }

  return (
    <PageFrame
      className="skill-upload-page"
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
      indexClassName="skill-upload-index"
      index={(
        <nav aria-label={t('skills.uploadProgressLabel')}>
          <div className="skill-upload-index__label">{t('skills.uploadOnThisPage')}</div>
          {UPLOAD_STEPS.map((uploadStep) => {
            const isCurrent = uploadStep.id === step
            const isComplete = uploadStep.id === 'package' && step !== 'package'
            const canOpen = uploadStep.id === 'package' || (uploadStep.id === 'validation' && readyForValidation)
            return (
              <button
                key={uploadStep.id}
                type="button"
                className="skill-upload-index__step"
                data-current={isCurrent}
                data-complete={isComplete}
                aria-current={isCurrent ? 'step' : undefined}
                disabled={!canOpen || step === 'publish'}
                onClick={() => { if (canOpen) setStep(uploadStep.id) }}
              >
                <span className="skill-upload-index__number" aria-hidden="true">
                  {isComplete ? <CheckIcon /> : uploadStep.number}
                </span>
                <span>{t(uploadStep.labelKey)}</span>
              </button>
            )
          })}
          <p className="skill-upload-index__note">{t('skills.uploadStepNote')}</p>
        </nav>
      )}
    >
      <div className="skill-upload-workspace">
        <SkillUploadForm
          step={step}
          onStepChange={setStep}
          onReadyChange={setReadyForValidation}
          onCancel={() => navigate('/skills')}
          onSubmit={submit}
        />
      </div>
    </PageFrame>
  )
}
