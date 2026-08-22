import { FileArchiveIcon, FileUpIcon, UploadCloudIcon, XIcon } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import type { SkillUploadInput, SkillUploadKind } from '@/lib/api'

type SkillUploadFormSubmit = (
  input: SkillUploadInput,
  reportProgress: (progress: number) => void,
) => Promise<void>

interface SkillUploadFormProps {
  onSubmit: SkillUploadFormSubmit
}

export function SkillUploadForm({ onSubmit }: SkillUploadFormProps) {
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

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    if (!domain.trim() || !slug.trim() || !name.trim() || !description.trim() || !file) {
      setError(t('skills.requiredFields'))
      return
    }
    if (kind === 'zip' && !file.name.toLowerCase().endsWith('.zip')) {
      setError(t('skills.invalidArchive'))
      return
    }

    setUploading(true)
    setProgress(0)
    try {
      await onSubmit(
        { domain: domain.trim(), slug: slug.trim(), name: name.trim(), description: description.trim(), kind, file },
        setProgress,
      )
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.uploadFailed'))
    } finally {
      setUploading(false)
    }
  }

  const isPackage = kind === 'zip'

  return (
    <form className="grid gap-6" onSubmit={(event) => void submit(event)}>
      {error && (
        <Alert variant="destructive">
          <AlertTitle>{t('skills.uploadFailed')}</AlertTitle>
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      <div className="grid gap-4 sm:grid-cols-2">
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
        <Field>
          <FieldLabel htmlFor="skill-upload-name">{t('skills.name')}</FieldLabel>
          <FieldContent>
            <Input id="skill-upload-name" value={name} onChange={(event) => setName(event.target.value)} placeholder={t('skills.namePlaceholder')} required />
          </FieldContent>
        </Field>
        <Field>
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

      <div>
        <p className="mb-2 text-sm font-medium">{t('skills.uploadKind')}</p>
        <div className="grid gap-3 sm:grid-cols-2" role="radiogroup" aria-label={t('skills.uploadKind')}>
          <button
            type="button"
            role="radio"
            aria-checked={isPackage}
            className={`rounded-xl border p-4 text-left transition ${isPackage ? 'border-primary bg-primary/10 shadow-sm' : 'border-border bg-muted/20 hover:border-primary/40'}`}
            onClick={() => changeKind('zip')}
          >
            <span className="flex items-center gap-2 text-sm font-medium"><FileArchiveIcon className="size-4 text-primary" />{t('skills.uploadPackage')}</span>
            <span className="mt-1 block text-xs leading-5 text-muted-foreground">{t('skills.uploadPackageHint')}</span>
          </button>
          <button
            type="button"
            role="radio"
            aria-checked={!isPackage}
            className={`rounded-xl border p-4 text-left transition ${!isPackage ? 'border-primary bg-primary/10 shadow-sm' : 'border-border bg-muted/20 hover:border-primary/40'}`}
            onClick={() => changeKind('file')}
          >
            <span className="flex items-center gap-2 text-sm font-medium"><FileUpIcon className="size-4 text-primary" />{t('skills.uploadSingleFile')}</span>
            <span className="mt-1 block text-xs leading-5 text-muted-foreground">{t('skills.uploadSingleFileHint')}</span>
          </button>
        </div>
      </div>

      <div>
        <p className="mb-2 text-sm font-medium">{isPackage ? t('skills.archive') : t('skills.singleFile')}</p>
        <label
          htmlFor="skill-upload-file"
          className={`group flex w-full flex-col items-center justify-center rounded-2xl border border-dashed px-6 py-10 text-center transition ${dragging ? 'border-primary bg-primary/10' : 'border-border bg-muted/20 hover:border-primary/50 hover:bg-primary/5'}`}
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
          <div className="flex size-12 items-center justify-center rounded-2xl bg-primary/10 text-primary transition group-hover:scale-105">
            {isPackage ? <FileArchiveIcon className="size-6" /> : <UploadCloudIcon className="size-6" />}
          </div>
          <span className="mt-3 text-sm font-medium">{isPackage ? t('skills.chooseArchive') : t('skills.chooseFile')}</span>
          <span className="mt-1 text-xs text-muted-foreground">{isPackage ? t('skills.archiveHint') : t('skills.singleFileHint')}</span>
        </label>
        {file && (
          <div className="mt-3 flex items-center gap-3 rounded-xl border border-border bg-card px-3 py-2.5">
            {isPackage ? <FileArchiveIcon className="size-4 shrink-0 text-[var(--hi)]" /> : <FileUpIcon className="size-4 shrink-0 text-[var(--hi)]" />}
            <span className="min-w-0 flex-1 truncate font-mono text-xs">{file.name}</span>
            <span className="shrink-0 text-[10px] text-muted-foreground">{Math.max(1, Math.round(file.size / 1024))} KB</span>
            <Button type="button" size="icon-xs" variant="ghost" aria-label={t('skills.removeFile', { name: file.name })} onClick={() => { setFile(null); if (inputRef.current) inputRef.current.value = '' }}>
              <XIcon />
            </Button>
          </div>
        )}
      </div>

      {uploading && (
        <div aria-live="polite" className="rounded-xl border border-primary/20 bg-primary/5 p-3">
          <div className="flex items-center justify-between gap-3 text-xs">
            <span>{t('skills.uploadProgress', { progress })}</span>
            <span className="font-mono text-muted-foreground">{progress}%</span>
          </div>
          <div className="mt-2 h-1.5 overflow-hidden rounded-full bg-primary/10">
            <div className="h-full rounded-full bg-primary transition-[width] duration-300" style={{ width: `${Math.max(progress, 4)}%` }} />
          </div>
        </div>
      )}

      <div className="flex justify-end">
        <Button type="submit" size="lg" disabled={uploading}>
          {uploading ? t('skills.uploading') : t('skills.uploadAction')}
        </Button>
      </div>
    </form>
  )
}
