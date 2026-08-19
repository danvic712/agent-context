import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowLeftIcon, ArrowRightIcon, CheckCircle2Icon, SparklesIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
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
    if (await validate()) {
      setStep(3)
    }
  }

  const stepDescription =
    step === 1
      ? t('wizard.stepAccountPreferencesDescription')
      : step === 2
        ? t('wizard.stepModelServiceDescription')
        : t('wizard.stepReviewDescription')

  return (
    <div className="flex min-h-svh items-center justify-center bg-muted/20 p-4">
      <Card className="w-full max-w-3xl">
        <CardHeader>
          <div className="mb-2 flex items-center justify-between gap-3">
            <Badge variant="outline">{t('wizard.stepCounter', { step })}</Badge>
            <div className="flex items-center gap-1.5" aria-label={t('wizard.progressLabel')}>
              {[1, 2, 3].map((item) => (
                <span
                  key={item}
                  className={`h-1.5 w-12 rounded-full ${item <= step ? 'bg-primary' : 'bg-border'}`}
                />
              ))}
            </div>
          </div>
          <CardTitle className="flex items-center gap-2">
            <SparklesIcon data-icon="inline-start" />
            {t('wizard.welcome')}
          </CardTitle>
          <CardDescription>{stepDescription}</CardDescription>
        </CardHeader>

        {step === 1 ? (
          <form onSubmit={submitAccount}>
            <CardContent>
              <FieldGroup>
                <Field>
                  <FieldLabel>{t('wizard.language')}</FieldLabel>
                  <FieldContent className="flex flex-row gap-2">
                    {languages.map((locale) => (
                      <Button
                        key={locale}
                        type="button"
                        variant={language === locale ? 'default' : 'outline'}
                        onClick={() => void chooseLanguage(locale)}
                        className="flex-1"
                      >
                        {locale === 'en-US' ? t('wizard.english') : t('wizard.chinese')}
                      </Button>
                    ))}
                  </FieldContent>
                </Field>
                <div className="grid gap-4 md:grid-cols-2">
                  <Field>
                    <FieldLabel htmlFor="display-name">{t('wizard.displayName')}</FieldLabel>
                    <FieldContent>
                      <Input
                        id="display-name"
                        value={account.displayName}
                        onChange={(event) => setAccount({ ...account, displayName: event.target.value })}
                        autoComplete="name"
                        placeholder={t('wizard.displayNamePlaceholder')}
                      />
                    </FieldContent>
                  </Field>
                  <Field>
                    <FieldLabel htmlFor="email">{t('wizard.email')}</FieldLabel>
                    <FieldContent>
                      <Input
                        id="email"
                        type="email"
                        value={account.email}
                        onChange={(event) => setAccount({ ...account, email: event.target.value })}
                        autoComplete="email"
                        placeholder={t('wizard.emailPlaceholder')}
                      />
                    </FieldContent>
                  </Field>
                </div>
                <Field>
                  <FieldLabel htmlFor="password">{t('wizard.password')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="password"
                      type="password"
                      value={account.password}
                      onChange={(event) => setAccount({ ...account, password: event.target.value })}
                      autoComplete="new-password"
                      placeholder={t('wizard.passwordPlaceholder')}
                    />
                  </FieldContent>
                </Field>
              </FieldGroup>
              {error && (
                <Alert variant="destructive" className="mt-4">
                  <AlertTitle>{t('wizard.setupFailed')}</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </CardContent>
            <CardFooter>
              <Button type="submit" className="w-full">
                {t('wizard.continue')}
                <ArrowRightIcon data-icon="inline-end" className="size-4" />
              </Button>
            </CardFooter>
          </form>
        ) : step === 2 ? (
          <form onSubmit={(event) => void submitModelService(event)}>
            <CardContent>
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
              {error && (
                <Alert variant="destructive" className="mt-4">
                  <AlertTitle>{t('wizard.setupFailed')}</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </CardContent>
            <CardFooter className="flex gap-2">
              <Button type="button" variant="outline" onClick={() => setStep(1)} disabled={validating}>
                <ArrowLeftIcon data-icon="inline-start" className="size-4" />
                {t('wizard.back')}
              </Button>
              <Button type="submit" disabled={validating} className="flex-1">
                {validating ? t('inference.verifying') : t('wizard.testAndReview')}
                <ArrowRightIcon data-icon="inline-end" className="size-4" />
              </Button>
            </CardFooter>
          </form>
        ) : (
          <form
            onSubmit={(event) => {
              event.preventDefault()
              void finish()
            }}
          >
            <CardContent className="space-y-5">
              <div className="rounded-xl border bg-muted/20 p-4">
                <div className="mb-3 flex items-center gap-2">
                  <CheckCircle2Icon className="size-4 text-primary" />
                  <h3 className="font-medium">{t('wizard.accountSummary')}</h3>
                </div>
                <dl className="grid gap-2 text-sm sm:grid-cols-2">
                  <div><dt className="text-muted-foreground">{t('wizard.displayName')}</dt><dd>{account.displayName}</dd></div>
                  <div><dt className="text-muted-foreground">{t('wizard.email')}</dt><dd>{account.email}</dd></div>
                  <div><dt className="text-muted-foreground">{t('wizard.language')}</dt><dd>{language}</dd></div>
                </dl>
              </div>
              <div className="rounded-xl border bg-muted/20 p-4">
                <div className="mb-3 flex items-center gap-2">
                  <CheckCircle2Icon className="size-4 text-primary" />
                  <h3 className="font-medium">{t('wizard.inferenceSummary')}</h3>
                </div>
                <div className="grid gap-3 md:grid-cols-2">
                  {draft.routes.map((route) => {
                    const provider = draft.providers.find((item) => item.id === route.providerId)
                    return (
                      <div key={route.id} className="rounded-lg border bg-background p-3 text-sm">
                        <p className="font-medium">{route.capability === 'Chat' ? t('inference.chatRoute') : t('inference.embeddingRoute')}</p>
                        <p className="text-muted-foreground">{provider?.name || t('inference.provider')} · {route.model}</p>
                      </div>
                    )
                  })}
                </div>
                <p className="mt-3 text-sm text-muted-foreground">{t('wizard.atomicCreateHint')}</p>
              </div>
              {error && (
                <Alert variant="destructive">
                  <AlertTitle>{t('wizard.setupFailed')}</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </CardContent>
            <CardFooter className="flex gap-2">
              <Button type="button" variant="outline" onClick={() => setStep(2)} disabled={submitting}>
                <ArrowLeftIcon data-icon="inline-start" className="size-4" />
                {t('wizard.back')}
              </Button>
              <Button type="submit" disabled={submitting || !validation?.valid} className="flex-1">
                {submitting ? t('wizard.settingUp') : t('wizard.createWorkspace')}
              </Button>
            </CardFooter>
          </form>
        )}
      </Card>
    </div>
  )
}
