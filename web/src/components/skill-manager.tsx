import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BookOpenIcon, PlusIcon } from 'lucide-react'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldContent, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Skeleton } from '@/components/ui/skeleton'
import { SkillPackageView } from '@/components/skill-package-view'
import {
  createSkill,
  getSkill,
  listSkills,
  publishSkill,
  readSkillFile,
  type SkillDetail,
  type SkillItem,
} from '@/lib/api'
import { cn } from '@/lib/utils'

const emptyDraft = { domain: 'dev', slug: '', name: '', description: '', instructions: '' }

export function SkillManager() {
  const { t } = useTranslation()
  const [items, setItems] = useState<SkillItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [detail, setDetail] = useState<SkillDetail | null>(null)
  const [loadingDetail, setLoadingDetail] = useState(false)
  const [creating, setCreating] = useState(false)
  const [draft, setDraft] = useState(emptyDraft)
  // Publish panel
  const [publishing, setPublishing] = useState<SkillDetail | null>(null)
  const [publishName, setPublishName] = useState('')
  const [publishDescription, setPublishDescription] = useState('')
  const [publishMain, setPublishMain] = useState('')

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      setItems(await listSkills())
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.failedLoad'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const select = async (item: SkillItem) => {
    setSelectedId(item.id)
    setCreating(false)
    setPublishing(null)
    setLoadingDetail(true)
    setError(null)
    try {
      setDetail(await getSkill(item.domainName, item.slug))
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.failedLoadOne'))
    } finally {
      setLoadingDetail(false)
    }
  }

  const create = async () => {
    setError(null)
    try {
      const created = await createSkill({
        domain: draft.domain.trim(),
        slug: draft.slug.trim(),
        name: draft.name.trim(),
        description: draft.description.trim(),
        instructions: draft.instructions,
      })
      setDraft(emptyDraft)
      setCreating(false)
      setDetail(created)
      setSelectedId(created.id)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.failedSave'))
    }
  }

  const openPublish = async (item: SkillDetail) => {
    // Prefill the editor with the current SKILL.md so publishing never wipes it.
    let main = ''
    try {
      main = await (await readSkillFile(item.id, 'SKILL.md')).text()
    } catch {
      main = ''
    }
    setPublishing(item)
    setPublishName(item.name)
    setPublishDescription(item.description)
    setPublishMain(main)
  }

  const submitPublish = async () => {
    if (!publishing) return
    setError(null)
    try {
      const updated = await publishSkill(publishing.id, {
        name: publishName.trim(),
        description: publishDescription.trim(),
        instructions: publishMain,
      })
      setPublishing(null)
      setDetail(updated)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.failedSave'))
    }
  }

  return (
    <div className="grid items-start gap-4 lg:grid-cols-[300px_minmax(0,1fr)]">
      {/* Skill list */}
      <Card className="overflow-hidden">
        <div className="flex items-center justify-between border-b border-border px-4 py-3">
          <span className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
            {t('appShell.tabs.skills')}
          </span>
          <Button size="sm" onClick={() => { setCreating(true); setPublishing(null); setSelectedId(null); setDetail(null) }}>
            <PlusIcon data-icon="inline-start" className="size-4" />
            {t('skills.newSkill')}
          </Button>
        </div>
        <CardContent className="p-0">
          {loading ? (
            <div className="flex flex-col gap-3 p-4" aria-busy="true">
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
              <Skeleton className="h-12 w-full" />
            </div>
          ) : items.length === 0 ? (
            <p className="p-4 text-sm text-muted-foreground">{t('skills.noSkillsHint')}</p>
          ) : (
            <div className="flex flex-col">
              {items.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => void select(item)}
                  className={cn(
                    'flex items-center gap-2.5 border-b border-border px-4 py-3 text-left transition-colors last:border-b-0',
                    selectedId === item.id ? 'bg-accent/15' : 'hover:bg-secondary',
                  )}
                >
                  <div className="flex size-8 shrink-0 items-center justify-center rounded-lg border border-border bg-secondary">
                    <BookOpenIcon className="size-4 text-muted-foreground" />
                  </div>
                  <div className="min-w-0">
                    <div className="truncate text-[13.5px] font-medium">{item.name}</div>
                    <div className="truncate font-mono text-[11px] text-muted-foreground">
                      {item.domainName} / {item.slug}
                    </div>
                  </div>
                  <Badge variant="outline" className="ml-auto shrink-0">
                    {t('skills.version', { version: item.version })}
                  </Badge>
                </button>
              ))}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Right pane: create form / publish form / package view / hint */}
      {creating ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('skills.createTitle')}</CardTitle>
            <CardDescription>{t('skills.createSkillDescription')}</CardDescription>
          </CardHeader>
          <CardContent>
            <form
              className="flex flex-col gap-3"
              onSubmit={(e) => {
                e.preventDefault()
                void create()
              }}
            >
              <div className="flex gap-3">
                <Field className="flex-1">
                  <FieldLabel>{t('skills.domain')}</FieldLabel>
                  <FieldContent>
                    <Input value={draft.domain} onChange={(e) => setDraft({ ...draft, domain: e.target.value })} placeholder={t('skills.domainPlaceholder')} />
                  </FieldContent>
                </Field>
                <Field className="flex-1">
                  <FieldLabel>{t('skills.slug')}</FieldLabel>
                  <FieldContent>
                    <Input value={draft.slug} onChange={(e) => setDraft({ ...draft, slug: e.target.value })} placeholder={t('skills.slugPlaceholder')} />
                  </FieldContent>
                </Field>
              </div>
              <Field>
                <FieldLabel>{t('skills.name')}</FieldLabel>
                <FieldContent>
                  <Input value={draft.name} onChange={(e) => setDraft({ ...draft, name: e.target.value })} placeholder={t('skills.namePlaceholder')} />
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel>{t('skills.description')}</FieldLabel>
                <FieldContent>
                  <Input value={draft.description} onChange={(e) => setDraft({ ...draft, description: e.target.value })} placeholder={t('skills.descriptionPlaceholder')} />
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel>{t('skills.instructions')}</FieldLabel>
                <FieldContent>
                  <Textarea value={draft.instructions} onChange={(e) => setDraft({ ...draft, instructions: e.target.value })} rows={10} placeholder={t('skills.instructionsPlaceholder')} />
                </FieldContent>
              </Field>
              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
              <div className="flex gap-2">
                <Button type="submit">
                  <PlusIcon data-icon="inline-start" className="size-4" />
                  {t('skills.create')}
                </Button>
                <Button type="button" variant="outline" onClick={() => { setCreating(false); setError(null) }}>
                  {t('skills.cancel')}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : publishing ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">{t('skills.publishTitle', { slug: publishing.slug })}</CardTitle>
            <CardDescription>{t('skills.publishDescription')}</CardDescription>
          </CardHeader>
          <CardContent>
            <form
              className="flex flex-col gap-3"
              onSubmit={(e) => {
                e.preventDefault()
                void submitPublish()
              }}
            >
              <Field>
                <FieldLabel>{t('skills.name')}</FieldLabel>
                <FieldContent>
                  <Input value={publishName} onChange={(e) => setPublishName(e.target.value)} />
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel>{t('skills.description')}</FieldLabel>
                <FieldContent>
                  <Input value={publishDescription} onChange={(e) => setPublishDescription(e.target.value)} />
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel>{t('skills.instructions')}</FieldLabel>
                <FieldContent>
                  <Textarea value={publishMain} onChange={(e) => setPublishMain(e.target.value)} rows={10} className="font-mono text-[12.5px]" />
                </FieldContent>
              </Field>
              {error && (
                <Alert variant="destructive">
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
              <div className="flex gap-2">
                <Button type="submit">{t('skills.publishNewVersion')}</Button>
                <Button type="button" variant="outline" onClick={() => setPublishing(null)}>
                  {t('skills.cancel')}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>
      ) : loadingDetail ? (
        <Card aria-busy="true">
          <CardHeader>
            <Skeleton className="h-5 w-40" />
            <Skeleton className="h-4 w-64" />
          </CardHeader>
          <CardContent className="flex flex-col gap-2">
            <Skeleton className="h-8 w-full" />
            <Skeleton className="h-40 w-full" />
          </CardContent>
        </Card>
      ) : detail ? (
        <SkillPackageView
          detail={detail}
          onChanged={setDetail}
          onDeleted={() => {
            setDetail(null)
            setSelectedId(null)
            void load()
          }}
          onPublish={openPublish}
        />
      ) : (
        <Card>
          <CardContent className="pt-8 text-center text-sm text-muted-foreground">
            {t('skills.selectHint')}
          </CardContent>
        </Card>
      )}
    </div>
  )
}
