import locale from 'i18next'
import { initReactI18next } from 'react-i18next'

// Keep each locale resource in its own i18next namespace. The namespace files
// preserve the existing dotted keys (e.g. `settings.pageTitle`) without
// creating another large aggregate JSON file.
import enUSCommon from '../../src/AgentContext.Application/locales/en-US/common.json'
import enUSSetup from '../../src/AgentContext.Application/locales/en-US/setup.json'
import enUSInference from '../../src/AgentContext.Application/locales/en-US/inference.json'
import enUSKnowledge from '../../src/AgentContext.Application/locales/en-US/knowledge.json'
import enUSSkills from '../../src/AgentContext.Application/locales/en-US/skills.json'
import enUSSettings from '../../src/AgentContext.Application/locales/en-US/settings.json'
import enUSErrors from '../../src/AgentContext.Application/locales/en-US/errors.json'
import zhCNCommon from '../../src/AgentContext.Application/locales/zh-CN/common.json'
import zhCNSetup from '../../src/AgentContext.Application/locales/zh-CN/setup.json'
import zhCNInference from '../../src/AgentContext.Application/locales/zh-CN/inference.json'
import zhCNKnowledge from '../../src/AgentContext.Application/locales/zh-CN/knowledge.json'
import zhCNSkills from '../../src/AgentContext.Application/locales/zh-CN/skills.json'
import zhCNSettings from '../../src/AgentContext.Application/locales/zh-CN/settings.json'
import zhCNErrors from '../../src/AgentContext.Application/locales/zh-CN/errors.json'

const namespaces = ['common', 'setup', 'inference', 'knowledge', 'skills', 'settings', 'errors']

void locale.use(initReactI18next).init({
  resources: {
    'en-US': {
      common: enUSCommon,
      setup: enUSSetup,
      inference: enUSInference,
      knowledge: enUSKnowledge,
      skills: enUSSkills,
      settings: enUSSettings,
      errors: enUSErrors,
    },
    'zh-CN': {
      common: zhCNCommon,
      setup: zhCNSetup,
      inference: zhCNInference,
      knowledge: zhCNKnowledge,
      skills: zhCNSkills,
      settings: zhCNSettings,
      errors: zhCNErrors,
    },
  },
  ns: namespaces,
  defaultNS: 'common',
  fallbackNS: namespaces.filter((namespace) => namespace !== 'common'),
  lng: 'en-US', // replaced at startup from GET /api/settings/language
  fallbackLng: 'en-US',
  interpolation: { escapeValue: false },
})

export default locale
