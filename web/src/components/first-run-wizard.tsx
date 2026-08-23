import { useRef, useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import {
  ArrowLeftIcon,
  ArrowRightIcon,
  CheckCircle2Icon,
  CheckIcon,
  LanguagesIcon,
  LockKeyholeIcon,
  SparklesIcon,
  UserRoundIcon,
} from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ActionBar, ActionBarStatus } from '@/components/ui/action-bar'
import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
import { SectionHeading, Surface } from '@/components/ui/surface'
import { StepIndicator, type StepIndicatorItem } from '@/components/ui/step-indicator'
import { ThemeToggle } from '@/components/theme-toggle'
import {
  createInferenceDraft,
  InferenceConfigForm,
  toInferenceInput,
  type InferenceDraft,
} from '@/components/inference-config-form'
import { postSetup, verifyInferenceConfiguration, type InferenceValidationResult } from '@/lib/api'
import i18n from '@/i18n'

interface FirstRunWizardProps {
  onComplete: () => void
}

interface AccountForm {
  displayName: string
  email: string
  password: string
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
  const [language, setLanguage] = useState<string>(i18n.language)
  const [validating, setValidating] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const completionStartedRef = useRef(false)

  const accountReady = Boolean(account.displayName.trim() && account.email.includes('@') && account.password.length >= 8)
  const serviceReady = validation?.valid === true
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
    if (completionStartedRef.current || submitting || !validation?.valid) return
    completionStartedRef.current = true
    setError(null)
    setSubmitting(true)
    try {
      await postSetup(
        account.displayName.trim(),
        account.email.trim(),
        account.password,
        language,
        toInferenceInput(draft),
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
    if (await validate()) setStep('review')
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
      : t('wizard.stepReviewDescription')

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
          <form className="setup-step-form" onSubmit={submitAccount}>
            <div className="setup-content-grid">
              <Surface as="aside" tone="muted" className="setup-assistant">
                <div className="setup-assistant__icon"><SparklesIcon aria-hidden="true" /></div>
                <SectionHeading
                  title={t('wizard.accountAsideTitle')}
                  description={t('wizard.accountAsideDescription')}
                  className="setup-assistant__heading"
                />
                <div className="setup-check-list">
                  <div className="setup-check-item"><UserRoundIcon aria-hidden="true" />{t('wizard.accountAsideItemAccount')}</div>
                  <div className="setup-check-item"><LanguagesIcon aria-hidden="true" />{t('wizard.accountAsideItemLanguage')}</div>
                  <div className="setup-check-item"><LockKeyholeIcon aria-hidden="true" />{t('wizard.accountAsideItemProtected')}</div>
                </div>
              </Surface>

              <Surface as="section" className="setup-form-surface" aria-labelledby="setup-account-title">
                <div className="setup-surface-header">
                  <SectionHeading
                    titleId="setup-account-title"
                    title={t('wizard.accountSummary')}
                    description={t('wizard.stepAccountDescription')}
                    aside={<span className="setup-section-badge">{t('wizard.stepOneLabel')}</span>}
                  />
                </div>
                <div className="setup-surface-body">
                  <div className="setup-field-grid">
                    <Field className="setup-field">
                      <FieldLabel htmlFor="display-name" className="setup-field-label">{t('wizard.displayName')}</FieldLabel>
                      <FieldContent>
                        <Input id="display-name" value={account.displayName} onChange={(event) => setAccount({ ...account, displayName: event.target.value })} autoComplete="name" placeholder={t('wizard.displayNamePlaceholder')} />
                      </FieldContent>
                    </Field>
                    <Field className="setup-field">
                      <FieldLabel htmlFor="email" className="setup-field-label">{t('wizard.email')}</FieldLabel>
                      <FieldContent>
                        <Input id="email" type="email" value={account.email} onChange={(event) => setAccount({ ...account, email: event.target.value })} autoComplete="email" placeholder={t('wizard.emailPlaceholder')} />
                      </FieldContent>
                    </Field>
                  </div>
                  <Field className="setup-field">
                    <FieldLabel htmlFor="password" className="setup-field-label">{t('wizard.password')}</FieldLabel>
                    <FieldContent>
                      <Input id="password" type="password" value={account.password} onChange={(event) => setAccount({ ...account, password: event.target.value })} autoComplete="new-password" placeholder={t('wizard.passwordPlaceholder')} />
                      <FieldDescription className="setup-field-description">{t('wizard.passwordHelp')}</FieldDescription>
                    </FieldContent>
                  </Field>
                  <Field className="setup-field">
                    <FieldLabel id="setup-language-label" className="setup-field-label">{t('wizard.language')}</FieldLabel>
                    <FieldContent>
                      <div id="setup-language" className="setup-language-options" role="group" aria-labelledby="setup-language-label">
                        {languages.map((locale) => (
                          <Button key={locale} type="button" variant="ghost" aria-pressed={language === locale} className="setup-language-option" onClick={() => void chooseLanguage(locale)}>
                            {locale === 'en-US' ? t('wizard.english') : t('wizard.chinese')}
                          </Button>
                        ))}
                      </div>
                    </FieldContent>
                  </Field>
                </div>
              </Surface>
            </div>

            {error && <Alert variant="destructive" className="setup-alert"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
            <ActionBar sticky className="setup-action-bar" status={<ActionBarStatus>{t('wizard.stepOneActionHint')}</ActionBarStatus>}>
              <Button type="submit" size="lg">{t('wizard.continue')} <ArrowRightIcon /></Button>
            </ActionBar>
          </form>
        ) : step === 'service' ? (
          <form className="setup-step-form" onSubmit={(event) => void submitModelService(event)}>
            <InferenceConfigForm
              className="setup-inference-form"
              draft={draft}
              onChange={(next) => {
                setDraft(next)
                setValidation(null)
              }}
              validation={validation}
              validating={validating}
              onValidate={() => void validate()}
              showVerifyAction={false}
              compact
            />
            {error && <Alert variant="destructive" className="setup-alert"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
            <ActionBar sticky className="setup-action-bar" status={<ActionBarStatus>{t('wizard.stepTwoActionHint')}</ActionBarStatus>}>
              <Button type="button" variant="ghost" onClick={() => { setError(null); setStep('account') }} disabled={validating}><ArrowLeftIcon />{t('wizard.back')}</Button>
              <Button type="submit" size="lg" disabled={validating}>{validating ? t('inference.verifying') : t('wizard.testAndReview')} <ArrowRightIcon /></Button>
            </ActionBar>
          </form>
        ) : (
          <form className="setup-step-form" onSubmit={(event) => { event.preventDefault(); void finish() }}>
            <div className="setup-review-layout">
              <Surface as="section" className="setup-review-surface" aria-labelledby="setup-review-title">
                <div className="setup-surface-header">
                  <SectionHeading titleId="setup-review-title" title={t('wizard.reviewTitle')} description={t('wizard.reviewDescription')} />
                </div>
                <div className="setup-surface-body setup-review-body">
                  <div className="setup-review-overview">
                    <div className="setup-review-item"><span>{t('wizard.displayName')}</span><strong>{account.displayName}</strong></div>
                    <div className="setup-review-item"><span>{t('wizard.language')}</span><strong>{language}</strong></div>
                    <div className="setup-review-item"><span>{t('inference.providersTitle')}</span><strong>{t('wizard.providerCount', { count: draft.providers.length })}</strong></div>
                  </div>

                  <div className="setup-review-block">
                    <div className="setup-review-block__heading"><UserRoundIcon aria-hidden="true" /><div><h3>{t('wizard.accountSummary')}</h3><p>{t('wizard.reviewAccountDescription')}</p></div></div>
                    <div className="setup-review-details">
                      <div><span>{t('wizard.displayName')}</span><strong>{account.displayName}</strong></div>
                      <div><span>{t('wizard.email')}</span><strong>{account.email}</strong></div>
                      <div><span>{t('wizard.language')}</span><strong>{language}</strong></div>
                    </div>
                  </div>

                  <div className="setup-review-block">
                    <div className="setup-review-block__heading"><SparklesIcon aria-hidden="true" /><div><h3>{t('wizard.inferenceSummary')}</h3><p>{t('wizard.reviewInferenceDescription')}</p></div><span className="setup-section-badge setup-section-badge--success"><CheckIcon aria-hidden="true" />{t('wizard.verifiedBeforeCreate')}</span></div>
                    <div className="setup-review-details">
                      {draft.routes.map((route) => {
                        const provider = draft.providers.find((item) => item.id === route.providerId)
                        return <div key={route.id}><span>{route.capability === 'Chat' ? t('inference.chatRoute') : t('inference.embeddingRoute')}</span><strong>{provider?.name || t('inference.provider')} · {route.model}</strong></div>
                      })}
                    </div>
                  </div>
                </div>
              </Surface>

              <Surface as="aside" tone="muted" className="setup-assistant setup-review-assistant">
                <div className="setup-assistant__icon setup-assistant__icon--success"><CheckCircle2Icon aria-hidden="true" /></div>
                <SectionHeading title={t('wizard.reviewAsideTitle')} description={t('wizard.atomicCreateHint')} className="setup-assistant__heading" />
                <div className="setup-check-list">
                  <div className="setup-check-item setup-check-item--success"><CheckIcon aria-hidden="true" /><span><strong>{t('wizard.accountSummary')}</strong><small>{account.email}</small></span></div>
                  <div className="setup-check-item setup-check-item--success"><CheckIcon aria-hidden="true" /><span><strong>{t('wizard.inferenceSummary')}</strong><small>{t('wizard.verifiedBeforeCreate')}</small></span></div>
                </div>
              </Surface>
            </div>

            {error && <Alert variant="destructive" className="setup-alert"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
            <ActionBar sticky className="setup-action-bar" status={<ActionBarStatus>{t('wizard.reviewActionHint')}</ActionBarStatus>}>
              <Button type="button" variant="ghost" onClick={() => { setError(null); setStep('service') }} disabled={submitting}><ArrowLeftIcon />{t('wizard.back')}</Button>
              <Button type="submit" size="lg" disabled={submitting || !serviceReady}>{submitting ? t('wizard.settingUp') : t('wizard.createWorkspace')}</Button>
            </ActionBar>
          </form>
        )}
      </div>
    </PageFrame>
  )
}
