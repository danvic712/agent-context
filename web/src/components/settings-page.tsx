import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LanguagesIcon, SettingsIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldContent, FieldLabel } from '@/components/ui/field'
import { Skeleton } from '@/components/ui/skeleton'
import {
  createInferenceDraft,
  InferenceConfigForm,
  toInferenceInput,
  type InferenceDraft,
} from '@/components/inference-config-form'
import {
  getInferenceConfiguration,
  getLanguage,
  saveInferenceConfiguration,
  saveLanguage,
  verifyInferenceConfiguration,
  type InferenceValidationResult,
} from '@/lib/api'
import i18n from '@/i18n'
import { useTheme } from '@/theme'

export function SettingsPage() {
  const { t } = useTranslation()
  const { mode, setMode } = useTheme()
  const [draft, setDraft] = useState<InferenceDraft | null>(null)
  const [validation, setValidation] = useState<InferenceValidationResult | null>(null)
  const [language, setLanguage] = useState<string>(i18n.language)
  const [loading, setLoading] = useState(true)
  const [validating, setValidating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      const [configuration, lang] = await Promise.all([getInferenceConfiguration(), getLanguage()])
      setDraft(createInferenceDraft(configuration))
      setValidation(null)
      setLanguage(lang.language)
      if (lang.language !== i18n.language) {
        await i18n.changeLanguage(lang.language)
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('settings.failedLoad'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const validate = async () => {
    if (!draft) return
    setError(null)
    setSaved(false)
    setValidating(true)
    try {
      setValidation(await verifyInferenceConfiguration(toInferenceInput(draft)))
    } catch (cause) {
      setValidation(null)
      setError(cause instanceof Error ? cause.message : t('settings.failedSave'))
    } finally {
      setValidating(false)
    }
  }

  const save = async () => {
    if (!draft || !validation?.valid) return
    setError(null)
    setSaved(false)
    setSaving(true)
    try {
      const result = await saveInferenceConfiguration(toInferenceInput(draft))
      setDraft(createInferenceDraft(result))
      setValidation(null)
      setSaved(true)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('settings.failedSave'))
    } finally {
      setSaving(false)
    }
  }

  const changeLanguage = async (locale: string) => {
    setError(null)
    try {
      await saveLanguage(locale)
      setLanguage(locale)
      await i18n.changeLanguage(locale)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('settings.failedSave'))
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="grid gap-4 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <SettingsIcon className="size-4 text-muted-foreground" />
              {t('settings.theme')}
            </CardTitle>
            <CardDescription>{t('settings.themeDescription')}</CardDescription>
          </CardHeader>
          <CardContent>
            <div role="radiogroup" aria-label={t('settings.theme')} className="flex flex-wrap gap-2">
              {(
                [
                  ['light', t('settings.themeLight')],
                  ['dark', t('settings.themeDark')],
                  ['system', t('settings.themeSystem')],
                ] as const
              ).map(([value, label]) => (
                <Button
                  key={value}
                  type="button"
                  role="radio"
                  aria-checked={mode === value}
                  variant={mode === value ? 'default' : 'outline'}
                  onClick={() => void setMode(value)}
                >
                  {label}
                </Button>
              ))}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-base">
              <LanguagesIcon className="size-4 text-muted-foreground" />
              {t('settings.language')}
            </CardTitle>
            <CardDescription>{t('settings.languageDescription')}</CardDescription>
          </CardHeader>
          <CardContent>
            <Field>
              <FieldLabel htmlFor="settings-language">{t('settings.language')}</FieldLabel>
              <FieldContent>
                <select
                  id="settings-language"
                  value={language}
                  onChange={(event) => void changeLanguage(event.target.value)}
                  className="flex h-9 w-full items-center rounded-lg border border-input bg-transparent px-3 text-sm shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50"
                >
                  <option value="en-US">{t('settings.english')}</option>
                  <option value="zh-CN">{t('settings.chinese')}</option>
                </select>
              </FieldContent>
            </Field>
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <SettingsIcon className="size-4 text-muted-foreground" />
            {t('settings.inferenceTitle')}
          </CardTitle>
          <CardDescription>{t('settings.inferenceDescription')}</CardDescription>
        </CardHeader>
        <CardContent>
          {loading || !draft ? (
            <div className="flex flex-col gap-3" aria-busy="true">
              <Skeleton className="h-5 w-44" />
              <Skeleton className="h-32 w-full" />
              <Skeleton className="h-32 w-full" />
            </div>
          ) : (
            <InferenceConfigForm
              draft={draft}
              onChange={(next) => {
                setDraft(next)
                setValidation(null)
                setSaved(false)
              }}
              validation={validation}
              validating={validating}
              onValidate={() => void validate()}
              onSave={() => void save()}
              saving={saving}
              saveDisabled={!validation?.valid}
            />
          )}
          {saved && <p className="mt-3 text-sm text-muted-foreground">{t('settings.saved')}</p>}
          {error && (
            <Alert variant="destructive" className="mt-4">
              <AlertTitle>{t('settings.saveFailed')}</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
