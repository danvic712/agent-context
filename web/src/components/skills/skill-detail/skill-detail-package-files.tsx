import { FileIcon, LoaderCircleIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { SkillFilePreview } from '../skill-file-preview'
import { SkillLanguageBadge } from '../skill-language-badge'
import type { SkillFileInfo } from '@/lib/api'
import { fileName } from './file-tree'
import { SkillDetailFileTree } from './skill-detail-file-tree'
import type { FileTreeNode } from './types'

interface SkillDetailPackageFilesProps {
  count: number
  nodes: FileTreeNode[]
  selectedPath: string
  selectedFile: SkillFileInfo | undefined
  selectedContent: string
  loadingSelectedFile: boolean
  downloading: boolean
  onSelect: (path: string) => void
  onDownload: () => void
}

export function SkillDetailPackageFiles({
  count,
  nodes,
  selectedPath,
  selectedFile,
  selectedContent,
  loadingSelectedFile,
  downloading,
  onSelect,
  onDownload,
}: SkillDetailPackageFilesProps) {
  const { t } = useTranslation()

  return (
    <section className="skill-detail-rail-card skill-detail-files-card ui-surface" aria-label={t('skills.packageTree')}>
      <header className="skill-detail-rail-card__header">
        <p className="skill-detail-rail-card__eyebrow">{t('skills.packageFilesTitle')}</p>
        <span className="skill-detail-rail-card__count">{t('skills.fileCount', { count })}</span>
      </header>
      <div className="skill-detail-files-card__body">
        <SkillDetailFileTree nodes={nodes} selectedPath={selectedPath} onSelect={onSelect} />
        <p className="skill-detail-files-card__note">{t('skills.supportingFilesNote')}</p>
        {selectedFile && selectedFile.path !== 'SKILL.md' && (
          <section className="skill-detail-support-preview" aria-label={selectedFile.path}>
            <header className="skill-detail-support-preview__header">
              <div className="min-w-0">
                <p className="skill-detail-support-preview__label">{t('skills.supportingFile')}</p>
                <h3 title={selectedFile.path}>{fileName(selectedFile.path)}</h3>
              </div>
              <SkillLanguageBadge path={selectedFile.path} content={selectedContent} />
            </header>
            {loadingSelectedFile ? (
              <div className="skill-detail-support-preview__loading" aria-busy="true">
                <LoaderCircleIcon className="size-3.5 animate-spin" />
                {t('skills.loadingFile')}
              </div>
            ) : selectedFile.binary ? (
              <div className="skill-detail-support-preview__empty">
                <FileIcon className="size-4" aria-hidden="true" />
                <span>{t('skills.binaryFileDownloadHint')}</span>
              </div>
            ) : (
              <div className="skill-detail-support-preview__body">
                <SkillFilePreview path={selectedFile.path} content={selectedContent} />
              </div>
            )}
            <Button type="button" size="sm" variant="outline" onClick={onDownload} disabled={downloading || selectedFile.binary === false && !selectedContent && loadingSelectedFile}>
              {t('skills.downloadFile')}
            </Button>
          </section>
        )}
      </div>
    </section>
  )
}
