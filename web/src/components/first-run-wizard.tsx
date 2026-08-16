import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowLeftIcon, ArrowRightIcon, SparklesIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { postSetup, saveLanguage, saveLlmOptions } from '@/lib/api'
import i18n from '@/i18n'

interface FirstRunWizardProps {
  onComplete: () => void
}

interface AccountForm {
  displayName: string
  email: string
  password: string
}

interface LlmForm {
  baseUrl: string
  apiKey: string
  model: string
  embeddingModel: string
}

const emptyAccount: AccountForm = { displayName: '', email: '', password: '' }
const emptyLlm: LlmForm = { baseUrl: '', apiKey: '', model: '', embeddingModel: '' }
const languages = ['en-US', 'zh-CN'] as const

export function FirstRunWizard({ onComplete }: FirstRunWizardProps) {
  const { t } = useTranslation()
  // 1 = language, 2 = account, 3 = LLM (optional). Language first so every later
  // step renders in the chosen platform language (T11).
  const [step, setStep] = useState<1 | 2 | 3>(1)
  const [account, setAccount] = useState<AccountForm>(emptyAccount)
  const [llm, setLlm] = useState<LlmForm>(emptyLlm)
  const [language, setLanguage] = useState<string>(i18n.language)
  const [savingLanguage, setSavingLanguage] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function chooseLanguage(locale: string) {
    setError(null)
    setSavingLanguage(true)
    try {
      await saveLanguage(locale)
      await i18n.changeLanguage(locale)
      setLanguage(locale)
    } catch (err) {
      setError(err instanceof Error ? err.message : t('wizard.failedGeneric'))
    } finally {
      setSavingLanguage(false)
    }
  }

  async function finish(configureLlm: boolean) {
    setError(null)
    setSubmitting(true)
    try {
      await postSetup(account.displayName.trim(), account.email.trim(), account.password)
      if (configureLlm) {
        await saveLlmOptions({
          baseUrl: llm.baseUrl.trim(),
          apiKey: llm.apiKey.trim(),
          model: llm.model.trim(),
          embeddingModel: llm.embeddingModel.trim() || null,
        })
      }
      onComplete()
    } catch (err) {
      setError(err instanceof Error ? err.message : t('wizard.failedGeneric'))
    } finally {
      setSubmitting(false)
    }
  }

  function submitAccount(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (!account.displayName.trim() || !account.email.trim() || account.password.length < 8) {
      setError(t('wizard.validationError'))
      return
    }

    setStep(3)
  }

  function submitLlm(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    void finish(true)
  }

  const stepDescription =
    step === 1
      ? t('wizard.stepLanguageDescription')
      : step === 2
        ? t('wizard.stepAccountDescription')
        : t('wizard.stepLlmDescription')

  return (
    <div className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <SparklesIcon data-icon="inline-start" />
            {t('wizard.welcome')}
          </CardTitle>
          <CardDescription>{stepDescription}</CardDescription>
        </CardHeader>

        {step === 1 ? (
          <form
            onSubmit={(e) => {
              e.preventDefault()
              setError(null)
              setStep(2)
            }}
          >
            <CardContent>
              <FieldGroup>
                <Field>
                  <FieldLabel>{t('wizard.stepLanguageTitle')}</FieldLabel>
                  <FieldContent className="flex flex-row gap-2">
                    {languages.map((locale) => (
                      <Button
                        key={locale}
                        type="button"
                        variant={language === locale ? 'default' : 'outline'}
                        onClick={() => void chooseLanguage(locale)}
                        disabled={savingLanguage}
                        className="flex-1"
                      >
                        {locale === 'en-US' ? t('wizard.english') : t('wizard.chinese')}
                      </Button>
                    ))}
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
              <Button type="submit" className="w-full" disabled={savingLanguage}>
                {t('wizard.continue')}
                <ArrowRightIcon data-icon="inline-end" className="size-4" />
              </Button>
            </CardFooter>
          </form>
        ) : step === 2 ? (
          <form onSubmit={submitAccount}>
            <CardContent>
              <FieldGroup>
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
        ) : (
          <form onSubmit={submitLlm}>
            <CardContent>
              <FieldGroup>
                <Field>
                  <FieldLabel htmlFor="llm-base-url">{t('wizard.baseUrl')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="llm-base-url"
                      value={llm.baseUrl}
                      onChange={(event) => setLlm({ ...llm, baseUrl: event.target.value })}
                      placeholder={t('wizard.baseUrlPlaceholder')}
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="llm-api-key">{t('wizard.apiKey')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="llm-api-key"
                      type="password"
                      value={llm.apiKey}
                      onChange={(event) => setLlm({ ...llm, apiKey: event.target.value })}
                      placeholder={t('wizard.apiKeyPlaceholder')}
                      autoComplete="off"
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="llm-model">{t('wizard.model')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="llm-model"
                      value={llm.model}
                      onChange={(event) => setLlm({ ...llm, model: event.target.value })}
                      placeholder={t('wizard.modelPlaceholder')}
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="llm-embedding-model">{t('wizard.embeddingModel')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="llm-embedding-model"
                      value={llm.embeddingModel}
                      onChange={(event) => setLlm({ ...llm, embeddingModel: event.target.value })}
                      placeholder={t('wizard.embeddingModelPlaceholder')}
                    />
                  </FieldContent>
                </Field>
              </FieldGroup>

              <Alert className="mt-4">
                <AlertTitle>{t('wizard.skipForNow')}</AlertTitle>
                <AlertDescription>{t('wizard.skipForNowDescription')}</AlertDescription>
              </Alert>

              {error && (
                <Alert variant="destructive" className="mt-4">
                  <AlertTitle>{t('wizard.setupFailed')}</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </CardContent>
            <CardFooter className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => setStep(2)}
                disabled={submitting}
              >
                <ArrowLeftIcon data-icon="inline-start" className="size-4" />
                {t('wizard.back')}
              </Button>
              <Button
                type="button"
                variant="ghost"
                onClick={() => void finish(false)}
                disabled={submitting}
              >
                {t('wizard.skip')}
              </Button>
              <Button type="submit" disabled={submitting} className="flex-1">
                {submitting ? t('wizard.settingUp') : t('wizard.createWorkspace')}
              </Button>
            </CardFooter>
          </form>
        )}
      </Card>
    </div>
  )
}
