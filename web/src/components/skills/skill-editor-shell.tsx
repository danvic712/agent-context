import type { ReactNode } from 'react'
import { ArrowLeftIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'

export function SkillEditorShell({ title, children }: { title: string; children: ReactNode }) {
  const { t } = useTranslation()
  return (
    <div className="mx-auto max-w-5xl">
      <div className="mb-5 flex items-center justify-between gap-3">
        <div><p className="kicker">{t('skills.editorKicker')}</p><h1 className="serif mt-1 text-3xl font-semibold">{title}</h1></div>
        <Link to="/skills" className="inline-flex h-8 items-center gap-1.5 rounded-lg px-2.5 text-xs font-medium text-muted-foreground hover:bg-muted hover:text-foreground"><ArrowLeftIcon className="size-3.5" />{t('skills.backToLibrary')}</Link>
      </div>
      {children}
    </div>
  )
}
