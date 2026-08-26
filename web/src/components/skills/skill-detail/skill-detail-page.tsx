import { useEffect, useMemo, useState } from 'react'
import { AlertCircleIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { useActionFeedback } from '@/components/ui/action-feedback'
import { PageFrame } from '@/components/ui/page-frame'
import { downloadSkillPackage, getSkillById, readSkillFile, type SkillDetail, type SkillFileInfo } from '@/lib/api'
import { skillSourceKey } from '../skill-source'
import { buildFileTree, fileName, sortFiles } from './file-tree'
import { SkillDetailActions } from './skill-detail-actions'
import { SkillDetailHeader } from './skill-detail-header'
import { SkillDetailMetadata } from './skill-detail-metadata'
import { SkillDetailPackageFiles } from './skill-detail-package-files'
import { SkillDetailReader } from './skill-detail-reader'
import { SkillDetailErrorState, SkillDetailLoadingState } from './skill-detail-states'
import './skill-detail.css'

export function SkillDetailPage() {
  const { t } = useTranslation()
  const { push } = useActionFeedback()
  const { id } = useParams<{ id: string }>()
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [selectedSupportPath, setSelectedSupportPath] = useState('')
  const [mainContent, setMainContent] = useState('')
  const [supportContent, setSupportContent] = useState('')
  const [loading, setLoading] = useState(true)
  const [loadingMain, setLoadingMain] = useState(false)
  const [loadingSupport, setLoadingSupport] = useState(false)
  const [downloadingPath, setDownloadingPath] = useState('')
  const [detailError, setDetailError] = useState<string | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)
  const packageDownloadKey = '__package__'

  useEffect(() => {
    if (!id) {
      setDetailError(t('skills.detailLoadFailed'))
      setLoading(false)
      return
    }

    let active = true
    setLoading(true)
    setDetail(null)
    setSelectedSupportPath('')
    setMainContent('')
    setSupportContent('')
    setDetailError(null)
    setFileError(null)
    void getSkillById(id)
      .then((loaded) => {
        if (!active) return
        setDetail(loaded)
      })
      .catch((cause) => {
        if (active) setDetailError(cause instanceof Error ? cause.message : t('skills.detailLoadFailed'))
      })
      .finally(() => {
        if (active) setLoading(false)
      })
    return () => { active = false }
  }, [id, t])

  const files = useMemo(() => sortFiles(detail?.manifest ?? []), [detail?.manifest])
  const fileTree = useMemo(() => buildFileTree(files, detail?.folders ?? []), [detail?.folders, files])
  const mainFile = files.find((file) => file.path === 'SKILL.md')
  const selectedSupportFile = files.find((file) => file.path === selectedSupportPath && file.path !== 'SKILL.md')
  const sourceLabel = detail ? t(skillSourceKey(detail.sourceType)) : ''

  useEffect(() => {
    if (!detail || !mainFile) return
    let active = true
    setLoadingMain(true)
    setMainContent('')
    setFileError(null)
    void readSkillFile(detail.id, mainFile.path)
      .then(async (blob) => {
        if (!active) return
        setMainContent(mainFile.binary ? '' : await blob.text())
      })
      .catch((cause) => {
        if (active) setFileError(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'))
      })
      .finally(() => {
        if (active) setLoadingMain(false)
      })
    return () => { active = false }
  }, [detail, mainFile, t])

  useEffect(() => {
    if (!detail || !selectedSupportFile) return
    let active = true
    setLoadingSupport(true)
    setSupportContent('')
    setFileError(null)
    void readSkillFile(detail.id, selectedSupportFile.path)
      .then(async (blob) => {
        if (!active) return
        setSupportContent(selectedSupportFile.binary ? '' : await blob.text())
      })
      .catch((cause) => {
        if (active) setFileError(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'))
      })
      .finally(() => {
        if (active) setLoadingSupport(false)
      })
    return () => { active = false }
  }, [detail, selectedSupportFile, t])

  const downloadFile = async (file: SkillFileInfo | undefined) => {
    if (!detail || !file || downloadingPath) return
    setDownloadingPath(file.path)
    try {
      const blob = await readSkillFile(detail.id, file.path)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = fileName(file.path)
      anchor.click()
      URL.revokeObjectURL(url)
      push(t('skills.fileDownloaded', { name: fileName(file.path) }), 'success')
    } catch (cause) {
      push(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'), 'error')
    } finally {
      setDownloadingPath('')
    }
  }

  const downloadMainPackage = async () => {
    if (!detail || !mainFile || downloadingPath) return
    setDownloadingPath(packageDownloadKey)
    try {
      const blob = detail.sourceType === 'zip'
        ? await downloadSkillPackage(detail.id)
        : await readSkillFile(detail.id, mainFile.path)
      const name = detail.sourceType === 'zip'
        ? `${detail.slug}-v${detail.version}.zip`
        : fileName(mainFile.path)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = name
      anchor.click()
      URL.revokeObjectURL(url)
      push(t('skills.fileDownloaded', { name }), 'success')
    } catch (cause) {
      push(cause instanceof Error ? cause.message : t('skills.fileLoadFailed'), 'error')
    } finally {
      setDownloadingPath('')
    }
  }

  const handleFileSelect = (path: string) => {
    setSelectedSupportPath(path === 'SKILL.md' ? '' : path)
  }

  const goBackToLibrary = () => {
    push(t('skills.backToLibraryFeedback'), 'info')
  }

  if (loading) return <SkillDetailLoadingState />
  if (detailError && !detail) return <SkillDetailErrorState error={detailError} />
  if (!detail) return <SkillDetailErrorState error={t('skills.detailLoadFailed')} />

  return (
    <PageFrame header={<SkillDetailHeader onBack={goBackToLibrary} />}>
      <section className="skill-detail-page" aria-label={detail.name}>
        <div className="skill-detail-layout">
          <div className="skill-detail-main">
            <SkillDetailReader
              file={mainFile}
              content={mainContent}
              loading={loadingMain}
            />
            {fileError && (
              <Alert variant="destructive" className="mt-4" role="alert">
                <AlertCircleIcon className="size-4" />
                <AlertTitle>{t('skills.fileLoadFailed')}</AlertTitle>
                <AlertDescription>{fileError}</AlertDescription>
              </Alert>
            )}
          </div>
          <aside className="skill-detail-rail" aria-label={t('skills.packageTree')}>
            <SkillDetailMetadata detail={detail} sourceLabel={sourceLabel} />
            <SkillDetailPackageFiles
              count={detail.manifest.length}
              nodes={fileTree}
              selectedPath={selectedSupportPath || mainFile?.path || ''}
              selectedFile={selectedSupportFile}
              selectedContent={supportContent}
              loadingSelectedFile={loadingSupport}
              downloading={downloadingPath === selectedSupportFile?.path}
              onSelect={handleFileSelect}
              onDownload={() => void downloadFile(selectedSupportFile)}
            />
            <SkillDetailActions
              mainFile={mainFile}
              isZipPackage={detail.sourceType === 'zip'}
              downloading={downloadingPath === packageDownloadKey}
              onDownload={() => void downloadMainPackage()}
            />
          </aside>
        </div>
      </section>
    </PageFrame>
  )
}
