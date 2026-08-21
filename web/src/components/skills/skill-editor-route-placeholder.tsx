import { ArrowLeftIcon, PencilLineIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router-dom'
import { Card, CardContent } from '@/components/ui/card'

export function SkillEditorRoutePlaceholder() {
  const { t } = useTranslation()
  const { id } = useParams<{ id?: string }>()

  return (
    <div className="mx-auto max-w-3xl">
      <Card className="overflow-hidden border-border/80 shadow-sm">
        <CardContent className="flex flex-col items-center px-6 py-16 text-center">
          <div className="flex size-14 items-center justify-center rounded-2xl bg-[var(--hi-soft)] text-[var(--hi)]">
            <PencilLineIcon className="size-7" />
          </div>
          <p className="kicker mt-5">{t('skills.editorKicker')}</p>
          <h1 className="serif mt-2 text-3xl font-semibold">{id ? t('skills.editorPreviewTitle') : t('skills.editorCreateTitle')}</h1>
          <p className="mt-3 max-w-lg text-sm leading-6 text-muted-foreground">{t('skills.editorDeferredDescription')}</p>
          <Link to="/skills" className="mt-6 inline-flex h-9 items-center gap-1.5 rounded-lg border border-border bg-background px-2.5 text-sm font-medium transition hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-3 focus-visible:ring-ring/50">
            <ArrowLeftIcon className="size-3.5" />{t('skills.backToLibrary')}
          </Link>
        </CardContent>
      </Card>
    </div>
  )
}
