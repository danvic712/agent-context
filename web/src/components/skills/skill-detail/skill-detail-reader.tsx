import { FileIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Badge } from '@/components/ui/badge'
import { SkillFilePreviewSkeleton } from '@/components/ui/loading-skeletons'
import { SkillFilePreview } from '../skill-file-preview'
import { SkillLanguageBadge } from '../skill-language-badge'
import type { SkillFileInfo } from '@/lib/api'
import { fileName } from './file-tree'

interface SkillDetailReaderProps {
  file: SkillFileInfo | undefined
  content: string
  loading: boolean
}

export function SkillDetailReader({ file, content, loading }: SkillDetailReaderProps) {
  const { t } = useTranslation()

  return (
    <section className="skill-detail-reader ui-surface" aria-label={t('skills.preview')}>
      <header className="skill-detail-reader__header">
        <div className="skill-detail-reader__file">
          <div className="skill-detail-reader__file-mark" aria-hidden="true"><FileIcon className="size-4" /></div>
          <div className="min-w-0">
            <h2>{file ? fileName(file.path) : 'SKILL.md'}</h2>
            <p>{file?.path ?? 'SKILL.md'} · {t('skills.renderedMarkdown')}</p>
          </div>
        </div>
        <div className="skill-detail-reader__tools">
          {file && <SkillLanguageBadge path={file.path} content={content} />}
          <Badge variant="outline">{t('skills.mainFile')}</Badge>
        </div>
      </header>
      <div className="skill-detail-reader__body">
        {!file ? (
          <div className="skill-detail-reader__empty">
            <FileIcon className="size-6" aria-hidden="true" />
            <span>{t('skills.emptyPackage')}</span>
          </div>
        ) : loading ? (
          <SkillFilePreviewSkeleton label={t('skills.loadingFile')} />
        ) : file.binary ? (
          <div className="skill-detail-reader__binary">
            <FileIcon className="size-6" aria-hidden="true" />
            <p>{t('skills.binaryFileNote')}</p>
            <span>{t('skills.binaryFileDownloadHint')}</span>
          </div>
        ) : (
          <SkillFilePreview path={file.path} content={content} />
        )}
      </div>
      <footer className="skill-detail-reader__footer">
        <span>{file ? `SKILL.md · ${file.size} ${t('skills.bytes')}` : t('skills.packageHasNoMainFile')}</span>
        <span>{file ? t('skills.mainPackageContent') : t('skills.packageHasNoMainFile')}</span>
      </footer>
    </section>
  )
}
