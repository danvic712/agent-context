import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LanguagesIcon, LoaderCircleIcon, MonitorIcon, MoonIcon, SunIcon } from 'lucide-react'
import { useLocation } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { EngineHealthPanel } from '@/components/engine-health'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
import { SectionHeading, Surface } from '@/components/ui/surface'
import { SettingsInferenceSkeleton } from '@/components/ui/loading-skeletons'
import { useActionFeedback } from '@/components/ui/action-feedback'
import { NativeSelect } from '@/components/ui/native-select'
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
import { useTheme, type ThemeMode } from '@/theme'
import './settings-page.css'

const settingsSectionIds = ['engine-health', 'preferences', 'inference'] as const

export function SettingsPage() {
  const { t } = useTranslation()
  const location = useLocation()
  const { mode, setMode } = useTheme()
  const { push } = useActionFeedback()
  const [draft, setDraft] = useState<InferenceDraft | null>(null)
  const [validation, setValidation] = useState<InferenceValidationResult | null>(null)
  const [language, setLanguage] = useState<string>(i18n.language)
  const [loading, setLoading] = useState(true)
  const [validating, setValidating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activeSection, setActiveSection] = useState<(typeof settingsSectionIds)[number]>('engine-health')

  useEffect(() => {
    const targetId = location.hash ? decodeURIComponent(location.hash.slice(1)) : 'engine-health'
    const sectionId = settingsSectionIds.find((id) => id === targetId) ?? 'engine-health'
    setActiveSection(sectionId)
    if (!location.hash) return
    const frame = window.requestAnimationFrame(() => {
      document.getElementById(sectionId)?.scrollIntoView({ block: 'start' })
    })
    return () => window.cancelAnimationFrame(frame)
  }, [location.hash])

  useEffect(() => {
    let frame = 0

    const updateActiveSection = () => {
      frame = 0
      let currentSection: (typeof settingsSectionIds)[number] = settingsSectionIds[0]
      for (const sectionId of settingsSectionIds) {
        const section = document.getElementById(sectionId)
        if (section && section.getBoundingClientRect().top <= 112) {
          currentSection = sectionId
        }
      }
      setActiveSection((current) => current === currentSection ? current : currentSection)
    }

    const scheduleUpdate = () => {
      if (frame) return
      frame = window.requestAnimationFrame(updateActiveSection)
    }

    scheduleUpdate()
    window.addEventListener('scroll', scheduleUpdate, { passive: true })
    window.addEventListener('resize', scheduleUpdate)
    return () => {
      window.removeEventListener('scroll', scheduleUpdate)
      window.removeEventListener('resize', scheduleUpdate)
      if (frame) window.cancelAnimationFrame(frame)
    }
  }, [])

  const load = useCallback(async () => {
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
  }, [t])

  useEffect(() => {
    void load()
  }, [load])

  const validate = async () => {
    if (!draft) return
    setError(null)
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
    setSaving(true)
    try {
      const result = await saveInferenceConfiguration(toInferenceInput(draft))
      setDraft(createInferenceDraft(result))
      setValidation(null)
      push(t('settings.saved'), 'success')
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

  const themeChoices: Array<[ThemeMode, string, React.ReactNode]> = [
    ['light', t('settings.themeLight'), <SunIcon key="light" />],
    ['dark', t('settings.themeDark'), <MoonIcon key="dark" />],
    ['system', t('settings.themeSystem'), <MonitorIcon key="system" />],
  ]

  return (
    <PageFrame
      className="settings-page"
      header={(
        <PageHeader
          eyebrow={t('settings.pageKicker')}
          title={t('settings.pageTitle')}
          description={t('settings.pageDescription')}
          actions={(
            <div className="settings-page__hero-meta">
              <span className="settings-page__badge"><span className="c-dot" />{t('settings.platformBadge')}</span>
              <span className="settings-page__badge settings-page__badge--muted">{t('settings.noRestart')}</span>
            </div>
          )}
        />
      )}
      indexClassName="settings-page__index"
      index={(
        <>
          <div className="settings-page__index-label">{t('settings.onThisPage')}</div>
          <a href="#engine-health" aria-current={activeSection === 'engine-health' ? 'location' : undefined}>{t('settings.runtimeSection')}</a>
          <a href="#preferences" aria-current={activeSection === 'preferences' ? 'location' : undefined}>{t('settings.preferencesSection')}</a>
          <a href="#inference" aria-current={activeSection === 'inference' ? 'location' : undefined}>{t('settings.inferenceSection')}</a>
          <p className="settings-page__index-note">{t('settings.sectionNavigationNote')}</p>
        </>
      )}
    >
      <div className="settings-page__content">
        <section id="engine-health" className="settings-page__section settings-page__section--runtime" aria-labelledby="settings-runtime-title">
          <SectionHeading
            title={t('settings.runtimeSection')}
            titleId="settings-runtime-title"
            description={t('settings.runtimeSectionHint')}
            className="settings-page__section-heading"
          />
          <EngineHealthPanel />
        </section>

        <section id="preferences" className="settings-page__section" aria-labelledby="settings-preferences-title">
          <SectionHeading
            title={t('settings.preferencesSection')}
            titleId="settings-preferences-title"
            description={t('settings.platformConfigurationHint')}
            className="settings-page__section-heading"
          />
          <div className="settings-page__preferences">
            <Surface className="settings-page__preference-card">
              <div className="settings-page__preference-heading">
                <div className="settings-page__preference-icon"><SunIcon aria-hidden="true" /></div>
                <div>
                  <h3>{t('settings.theme')}</h3>
                  <p>{t('settings.themeDescription')}</p>
                </div>
              </div>
              <div role="radiogroup" aria-label={t('settings.theme')} className="c-segmented">
                {themeChoices.map(([value, label, icon]) => (
                  <Button
                    key={value}
                    type="button"
                    role="radio"
                    aria-checked={mode === value}
                    variant="ghost"
                    className="c-segmented__item"
                    onClick={() => void setMode(value)}
                  >
                    {icon}
                    {label}
                  </Button>
                ))}
              </div>
            </Surface>

            <Surface className="settings-page__preference-card">
              <div className="settings-page__preference-heading">
                <div className="settings-page__preference-icon"><LanguagesIcon aria-hidden="true" /></div>
                <div>
                  <h3>{t('settings.language')}</h3>
                  <p>{t('settings.languageDescription')}</p>
                </div>
              </div>
              <div className="settings-page__language-field">
                <NativeSelect id="settings-language" className="c-select" aria-label={t('settings.language')} value={language} onChange={(event) => void changeLanguage(event.target.value)}>
                  <option value="en-US">{t('settings.english')}</option>
                  <option value="zh-CN">{t('settings.chinese')}</option>
                </NativeSelect>
              </div>
            </Surface>
          </div>
        </section>

        <section id="inference" className="settings-page__section" aria-labelledby="settings-inference-title">
          <SectionHeading
            title={t('settings.inferenceTitle')}
            titleId="settings-inference-title"
            description={t('settings.inferenceDescription')}
            className="settings-page__section-heading"
          />
          {!draft ? (
            <Surface className="settings-page__loading" aria-busy="true">
              <SettingsInferenceSkeleton label={t('common.loading')} />
            </Surface>
          ) : (
            <div className="settings-page__inference-shell" aria-busy={loading}>
              {loading && (
                <div className="settings-page__refreshing" role="status">
                  <LoaderCircleIcon aria-hidden="true" />
                  <span>{t('common.loading')}</span>
                </div>
              )}
              <InferenceConfigForm
                draft={draft}
                onChange={(next) => {
                  setDraft(next)
                  setValidation(null)
                }}
                validation={validation}
                validating={validating}
                onValidate={() => void validate()}
                onSave={() => void save()}
                onReset={() => void load()}
                saving={saving}
                saveDisabled={!validation?.valid}
              />
            </div>
          )}
        </section>
      </div>

      {error && (
        <Alert variant="destructive" className="c-validation-alert mt-4">
          <AlertTitle>{t('settings.saveFailed')}</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
    </PageFrame>
  )
}
