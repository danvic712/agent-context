import { ChevronDownIcon, LanguagesIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { useActionFeedback } from './action-feedback'
import { getUserFacingError, saveLanguage } from '@/lib/api'
import locale from '@/locale'

const languages = ['en-US', 'zh-CN'] as const

export function LanguageSwitcher() {
  const { t } = useTranslation()
  const { push } = useActionFeedback()
  const [saving, setSaving] = useState(false)
  const language = locale.resolvedLanguage === 'zh-CN' ? 'zh-CN' : 'en-US'

  const changeLanguage = async (nextLanguage: string) => {
    if (!languages.includes(nextLanguage as (typeof languages)[number]) || nextLanguage === language) return
    setSaving(true)
    try {
      await saveLanguage(nextLanguage)
      await locale.changeLanguage(nextLanguage)
      push(locale.t('appShell.languageChanged'), 'success')
    } catch (cause) {
      push(getUserFacingError(cause, t('appShell.languageChangeFailed')), 'error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <label className="ui-language-switcher" aria-label={t('appShell.language')}>
      <LanguagesIcon className="size-3.5 shrink-0" aria-hidden="true" />
      <span className="ui-language-switcher__value" aria-hidden="true">
        {t(language === 'zh-CN' ? 'appShell.languageChineseShort' : 'appShell.languageEnglishShort')}
      </span>
      <ChevronDownIcon className="ui-language-switcher__chevron" aria-hidden="true" />
      <select
        className="ui-language-switcher__control"
        value={language}
        onChange={(event) => void changeLanguage(event.target.value)}
        disabled={saving}
        aria-label={t('appShell.language')}
      >
        <option value="en-US">{t('appShell.languageEnglishShort')}</option>
        <option value="zh-CN">{t('appShell.languageChineseShort')}</option>
      </select>
    </label>
  )
}
