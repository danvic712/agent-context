import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LanguagesIcon, SettingsIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Field, FieldContent, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { getLanguage, getLlmOptions, saveLanguage, saveLlmOptions, type LlmOptionsDto } from '@/lib/api'
import i18n from '@/i18n'
import { useTheme } from '@/theme'

export function SettingsPage() {
  const { t } = useTranslation()
  const { mode, setMode } = useTheme()
  const [options, setOptions] = useState<LlmOptionsDto | null>(null)
  const [baseUrl, setBaseUrl] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [model, setModel] = useState('')
  const [embeddingModel, setEmbeddingModel] = useState('')
  const [language, setLanguage] = useState<string>(i18n.language)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      const [current, lang] = await Promise.all([getLlmOptions(), getLanguage()])
      setOptions(current)
      setBaseUrl(current.baseUrl ?? '')
      setModel(current.model ?? '')
      setEmbeddingModel(current.embeddingModel ?? '')
      setApiKey('')
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

  const save = async () => {
    setError(null)
    setSaved(false)
    try {
      const result = await saveLlmOptions({
        baseUrl: baseUrl.trim(),
        apiKey: apiKey.trim(),
        model: model.trim(),
        embeddingModel: embeddingModel.trim() || null,
      })
      setOptions(result)
      setApiKey('')
      setSaved(true)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('settings.failedSave'))
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
      {/* Theme + language side by side on wide screens, stacked otherwise */}
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
                  onChange={(e) => void changeLanguage(e.target.value)}
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

      {/* LLM endpoint — full width */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <SettingsIcon className="size-4 text-muted-foreground" />
            {t('settings.llmTitle')}
          </CardTitle>
          <CardDescription>{t('settings.llmDescription')}</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {loading ? (
            <div className="flex flex-col gap-3" aria-busy="true">
              <Skeleton className="h-5 w-44" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-full" />
              <Skeleton className="h-9 w-24" />
            </div>
          ) : (
            <>
              <div className="flex items-center gap-2">
                {options?.configured ? (
                  <Badge variant="default">{t('settings.configured')}</Badge>
                ) : (
                  <Badge variant="outline">{t('settings.notConfigured')}</Badge>
                )}
                {options?.configured && options.maskedApiKey && (
                  <Badge variant="secondary">{t('settings.keyMasked', { masked: options.maskedApiKey })}</Badge>
                )}
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <Field>
                  <FieldLabel htmlFor="settings-base-url">{t('settings.baseUrl')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="settings-base-url"
                      value={baseUrl}
                      onChange={(e) => setBaseUrl(e.target.value)}
                      placeholder={t('settings.baseUrlPlaceholder')}
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="settings-api-key">{t('settings.apiKey')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="settings-api-key"
                      type="password"
                      value={apiKey}
                      onChange={(e) => setApiKey(e.target.value)}
                      placeholder={
                        options?.configured ? t('settings.apiKeyUnchangedPlaceholder') : t('settings.apiKeyPlaceholder')
                      }
                      autoComplete="off"
                    />
                    {options?.configured && apiKey === '' && (
                      <p className="text-xs text-muted-foreground">{t('settings.apiKeyKeepHint')}</p>
                    )}
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="settings-model">{t('settings.model')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="settings-model"
                      value={model}
                      onChange={(e) => setModel(e.target.value)}
                      placeholder={t('settings.modelPlaceholder')}
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="settings-embedding-model">{t('settings.embeddingModel')}</FieldLabel>
                  <FieldContent>
                    <Input
                      id="settings-embedding-model"
                      value={embeddingModel}
                      onChange={(e) => setEmbeddingModel(e.target.value)}
                      placeholder={t('settings.embeddingModelPlaceholder')}
                    />
                  </FieldContent>
                </Field>
              </div>

              <div className="flex items-center gap-3">
                <Button size="sm" onClick={() => void save()}>
                  {t('settings.save')}
                </Button>
                {saved && <p className="text-sm text-muted-foreground">{t('settings.saved')}</p>}
              </div>

              {error && (
                <Alert variant="destructive">
                  <AlertTitle>{t('settings.saveFailed')}</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
