import { DownloadIcon, LoaderCircleIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import type { SkillFileInfo } from '@/lib/api'

interface SkillDetailActionsProps {
  mainFile: SkillFileInfo | undefined
  isZipPackage: boolean
  downloading: boolean
  onDownload: () => void
}

export function SkillDetailActions({ mainFile, isZipPackage, downloading, onDownload }: SkillDetailActionsProps) {
  const { t } = useTranslation()
  const downloadLabel = isZipPackage ? t('skills.downloadPackage') : t('skills.downloadMainFile')
  const downloadHint = isZipPackage ? t('skills.downloadPackageHint') : t('skills.downloadMainFileHint')

  return (
    <section className="skill-detail-actions-card ui-surface">
      <p className="skill-detail-rail-card__eyebrow">{downloadLabel}</p>
      <p>{downloadHint}</p>
      <div className="skill-detail-actions-card__actions">
        <Button type="button" size="sm" variant="outline" onClick={onDownload} disabled={!mainFile || downloading}>
          {downloading
            ? <LoaderCircleIcon data-icon="inline-start" className="size-3.5 animate-spin" />
            : <DownloadIcon data-icon="inline-start" className="size-3.5" />}
          {downloadLabel}
        </Button>
      </div>
    </section>
  )
}
