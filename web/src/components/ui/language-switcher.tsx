import { LanguagesIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useState } from 'react'
import { useActionFeedback } from './action-feedback'
import { NativeSelect } from './native-select'
import { saveLanguage } from '@/lib/api'
import i18n from '@/i18n'

const languages = ['en-US', 'zh-CN'] as const

export function LanguageSwitcher() {
  const { t } = useTranslation()
  const { push } = useActionFeedback()
  const [saving, setSaving] = useState(false)
  const language = i18n.resolvedLanguage === 'zh-CN' ? 'zh-CN' : 'en-US'

  const changeLanguage = async (nextLanguage: string) => {
    if (!languages.includes(nextLanguage as (typeof languages)[number]) || nextLanguage === language) return
    setSaving(true)
    try {
      await saveLanguage(nextLanguage)
      await i18n.changeLanguage(nextLanguage)
      push(i18n.t('appShell.languageChanged'), 'success')
    } catch (cause) {
      push(cause instanceof Error ? cause.message : t('appShell.languageChangeFailed'), 'error')
    } finally {
      setSaving(false)
    }
  }

  return (
    <label className="ui-language-switcher" aria-label={t('appShell.language')}>
      <LanguagesIcon className="size-3.5 shrink-0" aria-hidden="true" />
      <NativeSelect value={language} onChange={(event) => void changeLanguage(event.target.value)} disabled={saving} aria-label={t('appShell.language')} wrapperClassName="ui-language-switcher__select">
        <option value="en-US">{t('appShell.languageEnglishShort')}</option>
        <option value="zh-CN">{t('appShell.languageChineseShort')}</option>
      </NativeSelect>
    </label>
  )
}
