import { AlertCircleIcon, CheckCircle2Icon, CircleIcon, FileArchiveIcon, FileUpIcon, UploadCloudIcon, XIcon } from 'lucide-react'
import { useEffect, useRef, useState, type FormEvent, type KeyboardEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ActionBar, ActionBarStatus } from '@/components/ui/action-bar'
import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { SectionHeading, Surface } from '@/components/ui/surface'
import { Textarea } from '@/components/ui/textarea'
import { getUserFacingError, type SkillUploadInput, type SkillUploadKind } from '@/lib/api'

const MAX_FILE_BYTES = 10 * 1024 * 1024
const MAX_PACKAGE_BYTES = 50 * 1024 * 1024

export type SkillUploadStep = 'package' | 'validation' | 'publish'

type SkillUploadFormSubmit = (
  input: SkillUploadInput,
  reportProgress: (progress: number) => void,
) => Promise<void>

interface SkillUploadFormProps {
  step: SkillUploadStep
  onStepChange: (step: SkillUploadStep) => void
  onReadyChange: (ready: boolean) => void
  onCancel: () => void
  onSubmit: SkillUploadFormSubmit
}

function formatFileSize(bytes: number) {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  return `${Math.max(1, Math.round(bytes / 1024))} KB`
}

export function SkillUploadForm({ step, onStepChange, onReadyChange, onCancel, onSubmit }: SkillUploadFormProps) {
  const { t } = useTranslation()
  const inputRef = useRef<HTMLInputElement>(null)
  const [domain, setDomain] = useState('dev')
  const [slug, setSlug] = useState('')
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [kind, setKind] = useState<SkillUploadKind>('zip')
  const [file, setFile] = useState<File | null>(null)
  const [dragging, setDragging] = useState(false)
  const [uploading, setUploading] = useState(false)
  const [progress, setProgress] = useState(0)
  const [error, setError] = useState<string | null>(null)

  const isPackage = kind === 'zip'
  const metadataReady = Boolean(domain.trim() && slug.trim() && name.trim() && description.trim())
  const fileTypeReady = Boolean(file && (!isPackage || file.name.toLowerCase().endsWith('.zip')))
  const maxBytes = isPackage ? MAX_PACKAGE_BYTES : MAX_FILE_BYTES
  const fileSizeReady = Boolean(file && file.size <= maxBytes)
  const readyForValidation = metadataReady && fileTypeReady && fileSizeReady

  useEffect(() => {
    onReadyChange(readyForValidation)
  }, [onReadyChange, readyForValidation])

  const chooseFile = (selected: File | undefined) => {
    if (!selected) return
    setFile(selected)
    setError(null)
  }

  const changeKind = (nextKind: SkillUploadKind) => {
    setKind(nextKind)
    setFile(null)
    if (inputRef.current) inputRef.current.value = ''
    setError(null)
  }

  const validateDraft = () => {
    if (!metadataReady) return t('skills.requiredFields')
    if (!file) return t('skills.fileRequired')
    if (!fileTypeReady) return t('skills.invalidArchive')
    if (!fileSizeReady) return t('skills.fileTooLarge', { limit: isPackage ? '50 MB' : '10 MB' })
    return null
  }

  const continueToValidation = () => {
    const validationError = validateDraft()
    if (validationError) {
      setError(validationError)
      return
    }
    setError(null)
    onStepChange('validation')
  }

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (step !== 'validation') return

    const validationError = validateDraft()
    if (validationError) {
      setError(validationError)
      return
    }
    if (!file) return

    setError(null)
    setUploading(true)
    setProgress(0)
    onStepChange('publish')
    try {
      await onSubmit(
        { domain: domain.trim(), slug: slug.trim(), name: name.trim(), description: description.trim(), kind, file },
        setProgress,
      )
    } catch (cause) {
      setError(getUserFacingError(cause, t('skills.uploadFailed')))
      onStepChange('validation')
    } finally {
      setUploading(false)
    }
  }

  const onDropzoneKeyDown = (event: KeyboardEvent<HTMLLabelElement>) => {
    if (event.key !== 'Enter' && event.key !== ' ') return
    event.preventDefault()
    inputRef.current?.click()
  }

  const checks = [
    { ready: metadataReady, label: t('skills.validationMetadata') },
    { ready: fileTypeReady, label: t('skills.validationFile') },
    { ready: fileSizeReady, label: t('skills.validationSize', { limit: isPackage ? '50 MB' : '10 MB' }) },
    { ready: false, neutral: true, label: t('skills.validationServer') },
  ]

  return (
    <form className="skill-upload-form" onSubmit={(event) => void submit(event)}>
      {error && (
        <Alert variant="destructive" className="skill-upload-error">
          <AlertCircleIcon />
          <AlertTitle>{t('skills.uploadFailed')}</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="skill-upload-layout">
        <Surface as="section" className="skill-upload-panel">
          <div className="skill-upload-panel-head">
            <SectionHeading
              titleId="skill-upload-package-title"
              title={t('skills.choosePackageTitle')}
              description={t('skills.choosePackageDescription', { limit: isPackage ? '50 MB' : '10 MB' })}
            />
          </div>
          <div className="skill-upload-panel-body">
            <div>
              <p className="skill-upload-field-label">{isPackage ? t('skills.archive') : t('skills.singleFile')}</p>
              <label
                htmlFor="skill-upload-file"
                role="button"
                tabIndex={0}
                aria-label={isPackage ? t('skills.chooseArchive') : t('skills.chooseFile')}
                className={`skill-upload-dropzone ${dragging ? 'skill-upload-dropzone--dragging' : ''}`}
                onKeyDown={onDropzoneKeyDown}
                onDragEnter={(event) => { event.preventDefault(); setDragging(true) }}
                onDragOver={(event) => event.preventDefault()}
                onDragLeave={() => setDragging(false)}
                onDrop={(event) => { event.preventDefault(); setDragging(false); chooseFile(event.dataTransfer.files[0]) }}
              >
                <input
                  ref={inputRef}
                  id="skill-upload-file"
                  className="sr-only"
                  type="file"
                  accept={isPackage ? '.zip,application/zip' : undefined}
                  onChange={(event) => chooseFile(event.target.files?.[0])}
                />
                <span className="skill-upload-dropzone__icon" aria-hidden="true">
                  {isPackage ? <FileArchiveIcon /> : <UploadCloudIcon />}
                </span>
                <strong>{isPackage ? t('skills.chooseArchive') : t('skills.chooseFile')}</strong>
                <span>{isPackage ? t('skills.archiveHint') : t('skills.singleFileHint')}</span>
                <span className="skill-upload-dropzone__action">{t('skills.browseFiles')}</span>
              </label>
              {file && (
                <div className="skill-upload-file-row">
                  {isPackage ? <FileArchiveIcon aria-hidden="true" /> : <FileUpIcon aria-hidden="true" />}
                  <span className="skill-upload-file-name" title={file.name}>{file.name}</span>
                  <span className="skill-upload-file-size">{formatFileSize(file.size)}</span>
                  <Button
                    type="button"
                    size="icon"
                    variant="ghost"
                    aria-label={t('skills.removeFile', { name: file.name })}
                    onClick={() => { setFile(null); if (inputRef.current) inputRef.current.value = '' }}
                  >
                    <XIcon />
                  </Button>
                </div>
              )}
            </div>

            <fieldset className="skill-upload-kind-group">
              <legend className="skill-upload-field-label">{t('skills.uploadKind')}</legend>
              <div className="skill-upload-kind-grid" role="radiogroup" aria-label={t('skills.uploadKind')}>
                <button
                  type="button"
                  role="radio"
                  aria-checked={isPackage}
                  data-selected={isPackage}
                  className="skill-upload-kind-option"
                  onClick={() => changeKind('zip')}
                >
                  <span className="skill-upload-kind-option__title"><FileArchiveIcon />{t('skills.uploadPackage')}</span>
                  <span>{t('skills.uploadPackageHint')}</span>
                </button>
                <button
                  type="button"
                  role="radio"
                  aria-checked={!isPackage}
                  data-selected={!isPackage}
                  className="skill-upload-kind-option"
                  onClick={() => changeKind('file')}
                >
                  <span className="skill-upload-kind-option__title"><FileUpIcon />{t('skills.uploadSingleFile')}</span>
                  <span>{t('skills.uploadSingleFileHint')}</span>
                </button>
              </div>
            </fieldset>

            <div className="skill-upload-form-grid">
              <Field>
                <FieldLabel htmlFor="skill-upload-domain">{t('skills.domain')}</FieldLabel>
                <FieldContent>
                  <Input id="skill-upload-domain" value={domain} onChange={(event) => setDomain(event.target.value)} placeholder={t('skills.domainPlaceholder')} required />
                  <FieldDescription>{t('skills.uploadDomainHint')}</FieldDescription>
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel htmlFor="skill-upload-slug">{t('skills.slug')}</FieldLabel>
                <FieldContent>
                  <Input id="skill-upload-slug" value={slug} onChange={(event) => setSlug(event.target.value)} placeholder={t('skills.slugPlaceholder')} required />
                  <FieldDescription>{t('skills.uploadSlugHint')}</FieldDescription>
                </FieldContent>
              </Field>
              <Field className="skill-upload-field--wide">
                <FieldLabel htmlFor="skill-upload-name">{t('skills.name')}</FieldLabel>
                <FieldContent>
                  <Input id="skill-upload-name" value={name} onChange={(event) => setName(event.target.value)} placeholder={t('skills.namePlaceholder')} required />
                </FieldContent>
              </Field>
              <Field className="skill-upload-field--wide">
                <FieldLabel htmlFor="skill-upload-description">{t('skills.description')}</FieldLabel>
                <FieldContent>
                  <Textarea
                    id="skill-upload-description"
                    value={description}
                    onChange={(event) => setDescription(event.target.value)}
                    placeholder={t('skills.descriptionPlaceholder')}
                    rows={4}
                    className="min-h-28 resize-y leading-6"
                    required
                  />
                </FieldContent>
              </Field>
            </div>
          </div>
        </Surface>

        <Surface as="aside" tone="muted" className="skill-upload-check-panel" aria-labelledby="skill-upload-validation-title" aria-live="polite">
          <div className="skill-upload-panel-head">
            <SectionHeading
              titleId="skill-upload-validation-title"
              title={t('skills.beforePublishTitle')}
              description={t('skills.beforePublishDescription')}
            />
          </div>
          <div className="skill-upload-check-list">
            {checks.map((check) => (
              <div key={check.label} className={`skill-upload-check ${check.ready ? 'skill-upload-check--ready' : ''} ${check.neutral ? 'skill-upload-check--neutral' : ''}`}>
                {check.ready ? <CheckCircle2Icon aria-hidden="true" /> : check.neutral ? <CircleIcon aria-hidden="true" /> : <AlertCircleIcon aria-hidden="true" />}
                <span>{check.label}</span>
              </div>
            ))}
            <div className={`skill-upload-ready-note ${readyForValidation ? 'skill-upload-ready-note--ready' : ''}`}>
              {readyForValidation ? t('skills.validationReady') : t('skills.validationNotReady')}
            </div>
          </div>
        </Surface>
      </div>

      {uploading && (
        <div aria-live="polite" className="skill-upload-progress">
          <div className="skill-upload-progress__line">
            <span>{t('skills.uploadProgress', { progress })}</span>
            <span>{progress}%</span>
          </div>
          <div className="skill-upload-progress__track"><span style={{ width: `${Math.max(progress, 4)}%` }} /></div>
        </div>
      )}

      <ActionBar
        sticky
        className="skill-upload-actions"
        status={(
          <ActionBarStatus>
            {step === 'package' ? t('skills.uploadStatusPackage') : step === 'validation' ? t('skills.uploadStatusValidation') : t('skills.uploadStatusPublishing')}
          </ActionBarStatus>
        )}
      >
        <Button type="button" variant="ghost" onClick={onCancel} disabled={uploading}>{t('common.cancel')}</Button>
        {step === 'package' ? (
          <Button type="button" onClick={continueToValidation} disabled={!readyForValidation || uploading}>{t('skills.continueToValidation')}</Button>
        ) : step === 'validation' ? (
          <>
            <Button type="button" variant="outline" onClick={() => { setError(null); onStepChange('package') }} disabled={uploading}>{t('skills.backToPackage')}</Button>
            <Button type="submit" disabled={!readyForValidation || uploading}>{t('skills.publishSkill')}</Button>
          </>
        ) : (
          <Button type="button" disabled>{t('skills.publishingSkill')}</Button>
        )}
      </ActionBar>
    </form>
  )
}
