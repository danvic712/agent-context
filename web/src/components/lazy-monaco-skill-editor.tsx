import { lazy, Suspense } from 'react'
import { useTranslation } from 'react-i18next'
import type { MonacoSkillEditorProps } from './monaco-skill-editor'

const MonacoEditor = lazy(() => import('./monaco-skill-editor').then(({ MonacoSkillEditor: editor }) => ({ default: editor })))

export function LazyMonacoSkillEditor(props: MonacoSkillEditorProps) {
  const { t } = useTranslation()

  return (
    <Suspense
      fallback={
        <div className="flex h-[min(58vh,680px)] min-h-[360px] items-center justify-center rounded-xl border border-border/70 bg-[var(--code-bg)] text-xs text-muted-foreground" aria-busy="true">
          {t('skills.loadingEditor')}
        </div>
      }
    >
      <MonacoEditor {...props} />
    </Suspense>
  )
}
