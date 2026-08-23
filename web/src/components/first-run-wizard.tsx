import { useState } from 'react'
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
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { ActionBar, ActionBarStatus } from '@/components/ui/action-bar'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
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

export function FirstRunWizard({ onComplete }: FirstRunWizardProps) {
  const { t } = useTranslation()
  const [step, setStep] = useState<1 | 2 | 3>(1)
  const [account, setAccount] = useState<AccountForm>(emptyAccount)
  const [draft, setDraft] = useState<InferenceDraft>(() => createInferenceDraft())
  const [validation, setValidation] = useState<InferenceValidationResult | null>(null)
  const [language, setLanguage] = useState<string>(i18n.language)
  const [validating, setValidating] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const chooseLanguage = async (locale: string) => {
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
      setError(cause instanceof Error ? cause.message : t('wizard.failedGeneric'))
    } finally {
      setSubmitting(false)
    }
  }

  const submitAccount = (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    if (!account.displayName.trim() || !account.email.trim() || account.password.length < 8) {
      setError(t('wizard.validationError'))
      return
    }
    setStep(2)
  }

  const submitModelService = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (await validate()) setStep(3)
  }

  const pageTitle = step === 1 ? t('wizard.pageTitleAccount') : step === 2 ? t('wizard.pageTitleService') : t('wizard.pageTitleReview')
  const stepDescription =
    step === 1
      ? t('wizard.stepAccountPreferencesDescription')
      : step === 2
        ? t('wizard.stepModelServiceDescription')
        : t('wizard.stepReviewDescription')

  return (
    <PageFrame
      className="c-page c-page--setup"
      header={(
        <PageHeader
          eyebrow={t('wizard.pageKicker')}
          title={pageTitle}
          description={stepDescription}
          actions={(
            <div className="c-hero__aside">
              <span className="c-badge"><span className="c-dot" />{t('wizard.firstSetup')}</span>
              <span className="c-status-badge">{t('wizard.stepCounter', { step })}</span>
            </div>
          )}
        />
      )}
    >

      <div className="c-setup-progress" aria-label={t('wizard.progressLabel')}>
        {[1, 2, 3].map((item, index) => {
          const isDone = item < step
          const isCurrent = item === step
          return (
            <span key={item} className="contents">
              <button
                type="button"
                className={`c-progress-step ${isCurrent ? 'c-progress-step--current' : ''} ${isDone ? 'c-progress-step--done' : ''}`}
                onClick={() => {
                  if (item < step) setStep(item as 1 | 2 | 3)
                }}
              >
                {isDone ? <CheckIcon size={14} /> : <span className="c-progress-number">{item}</span>}
                {item === 1 ? t('wizard.stepAccountShort') : item === 2 ? t('wizard.stepServiceShort') : t('wizard.stepReviewShort')}
              </button>
              {index < 2 && <span className="c-progress-separator">/</span>}
            </span>
          )
        })}
      </div>

      {step === 1 ? (
        <form onSubmit={submitAccount}>
          <div className="c-layout">
            <aside className="c-readiness">
              <div className="c-readiness__icon"><SparklesIcon size={21} /></div>
              <h2 className="c-readiness__title">{t('wizard.accountAsideTitle')}</h2>
              <p className="c-readiness__description">{t('wizard.accountAsideDescription')}</p>
              <div className="c-account-aside__list">
                <div className="c-account-aside__item"><UserRoundIcon />{t('wizard.accountAsideItemAccount')}</div>
                <div className="c-account-aside__item"><LanguagesIcon />{t('wizard.accountAsideItemLanguage')}</div>
                <div className="c-account-aside__item"><LockKeyholeIcon />{t('wizard.accountAsideItemProtected')}</div>
              </div>
            </aside>

            <div className="c-stack">
              <section className="c-panel">
                <div className="c-panel__header">
                  <div>
                    <div className="c-panel__title"><UserRoundIcon /> {t('wizard.accountSummary')}</div>
                    <p className="c-panel__description">{t('wizard.stepAccountDescription')}</p>
                  </div>
                  <span className="c-status-badge">{t('wizard.stepOneLabel')}</span>
                </div>
                <div className="c-panel__body c-form-grid">
                  <div className="c-form-grid c-form-grid--two">
                    <label className="c-field" htmlFor="display-name">
                      <span className="c-field__label">{t('wizard.displayName')}</span>
                      <Input id="display-name" className="c-input" value={account.displayName} onChange={(event) => setAccount({ ...account, displayName: event.target.value })} autoComplete="name" placeholder={t('wizard.displayNamePlaceholder')} />
                    </label>
                    <label className="c-field" htmlFor="email">
                      <span className="c-field__label">{t('wizard.email')}</span>
                      <Input id="email" className="c-input" type="email" value={account.email} onChange={(event) => setAccount({ ...account, email: event.target.value })} autoComplete="email" placeholder={t('wizard.emailPlaceholder')} />
                    </label>
                  </div>
                  <label className="c-field" htmlFor="password">
                    <span className="c-field__label">{t('wizard.password')}</span>
                    <Input id="password" className="c-input" type="password" value={account.password} onChange={(event) => setAccount({ ...account, password: event.target.value })} autoComplete="new-password" placeholder={t('wizard.passwordPlaceholder')} />
                  </label>
                  <p className="c-form-help">{t('wizard.passwordHelp')}</p>
                  <div className="c-field">
                    <span className="c-field__label">{t('wizard.language')}</span>
                    <div className="c-segmented">
                      {languages.map((locale) => (
                        <Button key={locale} type="button" variant="ghost" className="c-segmented__item" aria-pressed={language === locale} onClick={() => void chooseLanguage(locale)}>
                          {locale === 'en-US' ? t('wizard.english') : t('wizard.chinese')}
                        </Button>
                      ))}
                    </div>
                  </div>
                </div>
              </section>
              {error && <Alert variant="destructive" className="c-validation-alert"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
              <ActionBar status={<ActionBarStatus>{t('wizard.stepOneActionHint')}</ActionBarStatus>}>
                <Button type="submit" className="c-button c-button--primary">{t('wizard.continue')} <ArrowRightIcon /></Button>
              </ActionBar>
            </div>
          </div>
        </form>
      ) : step === 2 ? (
        <form onSubmit={(event) => void submitModelService(event)}>
          <InferenceConfigForm
            draft={draft}
            onChange={(next) => {
              setDraft(next)
              setValidation(null)
            }}
            validation={validation}
            validating={validating}
            onValidate={() => void validate()}
            compact
          />
          {error && <Alert variant="destructive" className="c-validation-alert mt-4"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
          <ActionBar status={<ActionBarStatus>{t('wizard.stepTwoActionHint')}</ActionBarStatus>}>
            <Button type="button" variant="ghost" className="c-button c-button--ghost" onClick={() => setStep(1)} disabled={validating}><ArrowLeftIcon />{t('wizard.back')}</Button>
            <Button type="submit" className="c-button c-button--primary" disabled={validating}>{validating ? t('inference.verifying') : t('wizard.testAndReview')} <ArrowRightIcon /></Button>
          </ActionBar>
        </form>
      ) : (
        <form onSubmit={(event) => { event.preventDefault(); void finish() }}>
          <div className="c-review-card">
            <div>
              <div className="kicker c-kicker">{t('wizard.stepReviewShort')}</div>
              <h2 className="c-review-card__title">{t('wizard.reviewTitle')}</h2>
              <p className="c-review-card__description">{t('wizard.reviewDescription')}</p>
            </div>
            <div className="c-review-grid">
              <div className="c-review-item"><div className="c-review-item__label">{t('wizard.displayName')}</div><div className="c-review-item__value">{account.displayName}</div></div>
              <div className="c-review-item"><div className="c-review-item__label">{t('wizard.language')}</div><div className="c-review-item__value">{language}</div></div>
              <div className="c-review-item"><div className="c-review-item__label">{t('inference.providersTitle')}</div><div className="c-review-item__value">{t('wizard.providerCount', { count: draft.providers.length })}</div></div>
            </div>
          </div>
          <div className="c-layout">
            <aside className="c-readiness">
              <div className="c-readiness__icon"><CheckCircle2Icon size={21} /></div>
              <h2 className="c-readiness__title">{t('wizard.reviewAsideTitle')}</h2>
              <p className="c-readiness__description">{t('wizard.atomicCreateHint')}</p>
              <div className="c-checks">
                <div className="c-check c-check--ok"><CheckIcon className="c-check__icon" /><div><div className="c-check__label">{t('wizard.accountSummary')}</div><div className="c-check__detail">{account.email}</div></div></div>
                <div className="c-check c-check--ok"><CheckIcon className="c-check__icon" /><div><div className="c-check__label">{t('wizard.inferenceSummary')}</div><div className="c-check__detail">{t('wizard.verifiedBeforeCreate')}</div></div></div>
              </div>
            </aside>
            <div className="c-stack">
              <section className="c-panel">
                <div className="c-panel__header"><div><div className="c-panel__title"><UserRoundIcon /> {t('wizard.accountSummary')}</div><p className="c-panel__description">{t('wizard.reviewAccountDescription')}</p></div></div>
                <div className="c-panel__body c-review-grid">
                  <div className="c-review-item"><div className="c-review-item__label">{t('wizard.displayName')}</div><div className="c-review-item__value">{account.displayName}</div></div>
                  <div className="c-review-item"><div className="c-review-item__label">{t('wizard.email')}</div><div className="c-review-item__value">{account.email}</div></div>
                  <div className="c-review-item"><div className="c-review-item__label">{t('wizard.language')}</div><div className="c-review-item__value">{language}</div></div>
                </div>
              </section>
              <section className="c-panel">
                <div className="c-panel__header"><div><div className="c-panel__title"><SparklesIcon /> {t('wizard.inferenceSummary')}</div><p className="c-panel__description">{t('wizard.reviewInferenceDescription')}</p></div><span className="c-status-badge c-status-badge--ready"><span className="c-dot c-dot--ok" />{t('wizard.verifiedBeforeCreate')}</span></div>
                <div className="c-panel__body c-route-grid">
                  {draft.routes.map((route) => {
                    const provider = draft.providers.find((item) => item.id === route.providerId)
                    return <div key={route.id} className="c-review-item"><div className="c-review-item__label">{route.capability === 'Chat' ? t('inference.chatRoute') : t('inference.embeddingRoute')}</div><div className="c-review-item__value">{provider?.name || t('inference.provider')} · {route.model}</div></div>
                  })}
                </div>
              </section>
              {error && <Alert variant="destructive" className="c-validation-alert"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
              <ActionBar status={<ActionBarStatus>{t('wizard.reviewActionHint')}</ActionBarStatus>}>
                <Button type="button" variant="ghost" className="c-button c-button--ghost" onClick={() => setStep(2)} disabled={submitting}><ArrowLeftIcon />{t('wizard.back')}</Button>
                <Button type="submit" className="c-button c-button--primary" disabled={submitting || !validation?.valid}>{submitting ? t('wizard.settingUp') : t('wizard.createWorkspace')}</Button>
              </ActionBar>
            </div>
          </div>
        </form>
      )}
    </PageFrame>
  )
}
