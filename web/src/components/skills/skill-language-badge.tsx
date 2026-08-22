import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { languageLabelForSkillPath } from '@/lib/skill-language'

export function SkillLanguageBadge({ path, content = '' }: { path: string; content?: string }) {
  const { t } = useTranslation()
  const language = languageLabelForSkillPath(path, content)

  return (
    <Badge variant="outline" className="gap-1.5 font-mono text-[10px]" data-language-label={language}>
      <span className="text-muted-foreground">{t('skills.language')}</span>
      <span>{language}</span>
    </Badge>
  )
}
