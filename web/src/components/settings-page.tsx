import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { LanguagesIcon, MonitorIcon, MoonIcon, SunIcon } from 'lucide-react'
import { useLocation } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { EngineHealthPanel } from '@/components/engine-health'
import { PageFrame, PageHeader } from '@/components/ui/page-frame'
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

const settingsSectionIds = ['engine-health', 'preferences', 'inference'] as const

export function SettingsPage() {
  const { t } = useTranslation()
  const location = useLocation()
  const { mode, setMode } = useTheme()
  const [draft, setDraft] = useState<InferenceDraft | null>(null)
  const [validation, setValidation] = useState<InferenceValidationResult | null>(null)
  const [language, setLanguage] = useState<string>(i18n.language)
  const [loading, setLoading] = useState(true)
  const [validating, setValidating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)
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

  const themeChoices: Array<[ThemeMode, string, React.ReactNode]> = [
    ['light', t('settings.themeLight'), <SunIcon key="light" />],
    ['dark', t('settings.themeDark'), <MoonIcon key="dark" />],
    ['system', t('settings.themeSystem'), <MonitorIcon key="system" />],
  ]

  return (
    <PageFrame
      className="c-page"
      header={(
        <PageHeader
          eyebrow={t('settings.pageKicker')}
          title={t('settings.pageTitle')}
          description={t('settings.pageDescription')}
          actions={(
            <div className="c-hero__aside">
              <span className="c-badge"><span className="c-dot" />{t('settings.platformBadge')}</span>
              <span className="c-status-badge">{t('settings.noRestart')}</span>
            </div>
          )}
        />
      )}
      indexClassName="c-settings-index"
      index={(
        <>
          <div className="c-settings-index__label">{t('settings.onThisPage')}</div>
          <a href="#engine-health" aria-current={activeSection === 'engine-health' ? 'location' : undefined}>{t('settings.runtimeSection')}</a>
          <a href="#preferences" aria-current={activeSection === 'preferences' ? 'location' : undefined}>{t('settings.preferencesSection')}</a>
          <a href="#inference" aria-current={activeSection === 'inference' ? 'location' : undefined}>{t('settings.inferenceSection')}</a>
          <p className="c-settings-index__note">{t('settings.sectionNavigationNote')}</p>
        </>
      )}
    >

      <div className="c-settings-stack">
          <div className="c-section-label">
            <h2>{t('settings.runtimeSection')}</h2>
            <p className="c-runtime-caption"><span className="c-dot c-dot--ok" />{t('settings.runtimeSectionHint')}</p>
          </div>
          <EngineHealthPanel />

          <div className="c-section-label">
            <h2>{t('settings.platformConfigurationSection')}</h2>
            <p>{t('settings.platformConfigurationHint')}</p>
          </div>

          <div id="preferences" className="c-preferences c-settings-anchor">
            <section className="c-panel">
              <div className="c-panel__header">
                <div>
                  <div className="c-panel__title"><SunIcon /> {t('settings.theme')}</div>
                  <p className="c-panel__description">{t('settings.themeDescription')}</p>
                </div>
              </div>
              <div className="c-preference__content">
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
              </div>
            </section>

            <section className="c-panel">
              <div className="c-panel__header">
                <div>
                  <div className="c-panel__title"><LanguagesIcon /> {t('settings.language')}</div>
                  <p className="c-panel__description">{t('settings.languageDescription')}</p>
                </div>
              </div>
              <div className="c-preference__content">
                <label className="c-field" htmlFor="settings-language">
                  <span className="c-field__label">{t('settings.language')}</span>
                  <select id="settings-language" className="c-select" value={language} onChange={(event) => void changeLanguage(event.target.value)}>
                    <option value="en-US">{t('settings.english')}</option>
                    <option value="zh-CN">{t('settings.chinese')}</option>
                  </select>
                </label>
              </div>
            </section>
          </div>

          <section id="inference" className="c-settings-anchor">
            {loading || !draft ? (
              <div className="c-panel p-5" aria-busy="true">
                <div className="flex flex-col gap-3">
                  <Skeleton className="h-5 w-44" />
                  <Skeleton className="h-32 w-full" />
                  <Skeleton className="h-32 w-full" />
                </div>
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
                onReset={() => void load()}
                saving={saving}
                saveDisabled={!validation?.valid}
              />
            )}
          </section>
      </div>

      {saved && <p className="mt-3 text-xs text-ok">{t('settings.saved')}</p>}
      {error && (
        <Alert variant="destructive" className="c-validation-alert mt-4">
          <AlertTitle>{t('settings.saveFailed')}</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}
    </PageFrame>
  )
}
