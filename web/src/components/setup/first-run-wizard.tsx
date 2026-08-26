import { useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { LanguagesIcon } from 'lucide-react'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
import { StepIndicator, type StepIndicatorItem } from '@/components/ui/step-indicator'
import { ThemeToggle } from '@/components/theme-toggle'
import { createInferenceDraft, toInferenceInput, type InferenceDraft } from '@/components/inference-config-form'
import { postSetup, verifyInferenceConfiguration, type InferenceValidationResult } from '@/lib/api'
import i18n from '@/i18n'
import { AccountStep } from './steps/account-step'
import { ModelServiceStep } from './steps/model-service-step'
import { ReviewStep } from './steps/review-step'
import type { AccountForm } from './types'
import './first-run-wizard.css'

interface FirstRunWizardProps {
  onComplete: () => void
}

const emptyAccount: AccountForm = { displayName: '', email: '', password: '' }
const languages = ['en-US', 'zh-CN'] as const

const setupSteps = [
  { id: 'account', labelKey: 'wizard.stepAccountShort' },
  { id: 'service', labelKey: 'wizard.stepServiceShort' },
  { id: 'review', labelKey: 'wizard.stepReviewShort' },
] as const

type SetupStep = (typeof setupSteps)[number]['id']

export function FirstRunWizard({ onComplete }: FirstRunWizardProps) {
  const { t } = useTranslation()
  const [step, setStep] = useState<SetupStep>('account')
  const [account, setAccount] = useState<AccountForm>(emptyAccount)
  const [draft, setDraft] = useState<InferenceDraft>(() => createInferenceDraft())
  const [validation, setValidation] = useState<InferenceValidationResult | null>(null)
  const [inferenceSkipped, setInferenceSkipped] = useState(false)
  const [language, setLanguage] = useState<string>(i18n.language)
  const [validating, setValidating] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const completionStartedRef = useRef(false)

  const accountReady = Boolean(account.displayName.trim() && account.email.includes('@') && account.password.length >= 8)
  const serviceReady = inferenceSkipped || validation?.valid === true
  const stepItems: StepIndicatorItem[] = setupSteps.map((item) => ({ id: item.id, label: t(item.labelKey) }))
  const completedSteps: string[] = []
  if (accountReady && step !== 'account') completedSteps.push('account')
  if (serviceReady && step === 'review') completedSteps.push('service')

  const canOpenStep = (nextStep: string) => {
    if (nextStep === 'account') return true
    if (nextStep === 'service') return accountReady
    return serviceReady
  }

  const chooseStep = (nextStep: string) => {
    if (!canOpenStep(nextStep) || submitting || validating) return
    setError(null)
    setStep(nextStep as SetupStep)
  }

  const chooseLanguage = async (locale: string) => {
    if (!languages.includes(locale as (typeof languages)[number]) || locale === language) return
    setError(null)
    try {
      await i18n.changeLanguage(locale)
      setLanguage(locale)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('wizard.failedGeneric'))
    }
  }

  const validate = async () => {
    setError(null)
    setInferenceSkipped(false)
    setValidating(true)
    try {
      const result = await verifyInferenceConfiguration(toInferenceInput(draft))
      setValidation(result)
      return result.valid
    } catch (cause) {
      setValidation(null)
      setError(cause instanceof Error ? cause.message : t('wizard.failedGeneric'))
      return false
    } finally {
      setValidating(false)
    }
  }

  const finish = async () => {
    if (completionStartedRef.current || submitting || !serviceReady) return
    completionStartedRef.current = true
    setError(null)
    setSubmitting(true)
    try {
      await postSetup(
        account.displayName.trim(),
        account.email.trim(),
        account.password,
        language,
        inferenceSkipped ? undefined : toInferenceInput(draft),
      )
      onComplete()
    } catch (cause) {
      completionStartedRef.current = false
      setError(cause instanceof Error ? cause.message : t('wizard.failedGeneric'))
    } finally {
      setSubmitting(false)
    }
  }

  const submitAccount = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    if (!accountReady) {
      setError(t('wizard.validationError'))
      return
    }
    setStep('service')
  }

  const submitModelService = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    // The form starts with provider suggestions and empty route/API-key
    // values. Treat that untouched state as an intentional deferred setup so
    // the first-run flow does not send an incomplete inference draft.
    const hasInferenceInput = draft.routes.some((route) => route.model.trim()) ||
      draft.providers.some((provider) => provider.apiKey.trim())
    if (!hasInferenceInput) {
      skipInference()
      return
    }
    if (await validate()) setStep('review')
  }

  const skipInference = () => {
    if (validating || submitting) return
    setError(null)
    setValidation(null)
    setInferenceSkipped(true)
    setStep('review')
  }

  const pageTitle = step === 'account'
    ? t('wizard.pageTitleAccount')
    : step === 'service'
      ? t('wizard.pageTitleService')
      : t('wizard.pageTitleReview')
  const stepDescription = step === 'account'
    ? t('wizard.stepAccountPreferencesDescription')
    : step === 'service'
      ? t('wizard.stepModelServiceDescription')
      : t(inferenceSkipped ? 'wizard.reviewSkippedDescription' : 'wizard.stepReviewDescription')

  return (
    <PageFrame
      className="setup-page"
      header={(
        <PageHeader
          eyebrow={t('wizard.pageKicker')}
          title={pageTitle}
          description={stepDescription}
          actions={(
            <div className="setup-header-actions">
              <div className="setup-header-tools">
                <label className="setup-locale-control">
                  <LanguagesIcon aria-hidden="true" />
                  <span className="sr-only">{t('wizard.language')}</span>
                  <select value={language} onChange={(event) => void chooseLanguage(event.target.value)} aria-label={t('wizard.language')}>
                    <option value="en-US">{t('wizard.english')}</option>
                    <option value="zh-CN">{t('wizard.chinese')}</option>
                  </select>
                </label>
                <ThemeToggle />
              </div>
              <div className="setup-header-meta">
                <span className="setup-step-counter">{t('wizard.stepCounter', { step: setupSteps.findIndex((item) => item.id === step) + 1 })}</span>
              </div>
            </div>
          )}
        />
      )}
    >
      <div className="setup-workspace">
        <StepIndicator
          steps={stepItems}
          currentId={step}
          completedIds={completedSteps}
          ariaLabel={t('wizard.progressLabel')}
          onSelect={chooseStep}
          isSelectable={canOpenStep}
          disabled={submitting || validating}
        />

        {step === 'account' ? (
          <AccountStep
            account={account}
            error={error}
            language={language}
            onAccountChange={setAccount}
            onLanguageChange={chooseLanguage}
            onSubmit={submitAccount}
          />
        ) : step === 'service' ? (
          <ModelServiceStep
            draft={draft}
            error={error}
            validating={validating}
            validation={validation}
            onBack={() => { setError(null); setStep('account') }}
            onChange={(next) => {
              setDraft(next)
              setValidation(null)
              setInferenceSkipped(false)
            }}
            onSubmit={(event) => void submitModelService(event)}
            onValidate={() => void validate()}
            onSkip={skipInference}
          />
        ) : (
          <ReviewStep
            account={account}
            draft={draft}
            error={error}
            language={language}
            serviceReady={serviceReady}
            inferenceSkipped={inferenceSkipped}
            submitting={submitting}
            onBack={() => { setError(null); setInferenceSkipped(false); setStep('service') }}
            onFinish={finish}
          />
        )}
      </div>
    </PageFrame>
  )
}
