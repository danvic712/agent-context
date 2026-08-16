import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
// The same single JSON store the backend embeds (ADR 0008): one file per locale,
// namespaced ui / errors / prompts. The frontend only consumes the `ui` block.
import enUS from '../../i18n/en-US.json'
import zhCN from '../../i18n/zh-CN.json'

// Keys arrive as dotted strings (e.g. "appShell.tabs.knowledge"); translate the
// whole `ui` block so `t('appShell.tabs.knowledge')` resolves to the nested key.
const ui = (resources: typeof enUS) => resources.ui

void i18n.use(initReactI18next).init({
  resources: {
    'en-US': { translation: ui(enUS) },
    'zh-CN': { translation: ui(zhCN) },
  },
  lng: 'en-US', // replaced at startup from GET /api/settings/language
  fallbackLng: 'en-US',
  interpolation: { escapeValue: false },
})

export default i18n
