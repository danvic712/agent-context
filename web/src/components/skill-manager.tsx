import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { BookOpenIcon, PlusIcon, SendIcon, TrashIcon } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Field, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import {
  createSkill,
  deleteSkill,
  getSkill,
  listSkills,
  publishSkill,
  type SkillItem,
} from '@/lib/api'

interface Draft {
  domain: string
  slug: string
  name: string
  description: string
  instructions: string
}

const emptyDraft: Draft = { domain: 'dev', slug: '', name: '', description: '', instructions: '' }

export function SkillManager() {
  const { t } = useTranslation()
  const [items, setItems] = useState<SkillItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [editing, setEditing] = useState<SkillItem | null>(null)
  const [draft, setDraft] = useState<Draft>(emptyDraft)

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

  const startEdit = async (item: SkillItem) => {
    setError(null)
    try {
      const detail = await getSkill(item.domainName, item.slug)
      setEditing(item)
      setDraft({
        domain: detail.domainName,
        slug: detail.slug,
        name: detail.name,
        description: detail.description,
        instructions: detail.instructions,
      })
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.failedLoadOne'))
    }
  }

  const save = async () => {
    setError(null)
    try {
      if (editing) {
        // Publish a new version on top of the current latest (AC2).
        await publishSkill(editing.id, {
          name: draft.name,
          description: draft.description,
          instructions: draft.instructions,
        })
      } else {
        await createSkill(draft)
      }
      setEditing(null)
      setDraft(emptyDraft)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.failedSave'))
    }
  }

  const remove = async (item: SkillItem) => {
    if (!window.confirm(t('skills.deleteConfirm', { slug: item.slug }))) {
      return
    }
    setError(null)
    try {
      await deleteSkill(item.id)
      await load()
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : t('skills.failedDelete'))
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">
            {editing
              ? t('skills.publishTitle', { slug: editing.slug })
              : t('skills.createTitle')}
          </CardTitle>
          <CardDescription>
            {editing ? t('skills.publishDescription') : t('skills.createDescription')}
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form
            className="flex flex-col gap-3"
            onSubmit={(e) => {
              e.preventDefault()
              void save()
            }}
          >
            <div className="flex gap-3">
              <Field className="flex-1">
                <FieldLabel>{t('skills.domain')}</FieldLabel>
                <Input
                  value={draft.domain}
                  onChange={(e) => setDraft({ ...draft, domain: e.target.value })}
                  disabled={editing !== null}
                  placeholder={t('skills.domainPlaceholder')}
                />
              </Field>
              <Field className="flex-1">
                <FieldLabel>{t('skills.slug')}</FieldLabel>
                <Input
                  value={draft.slug}
                  onChange={(e) => setDraft({ ...draft, slug: e.target.value })}
                  disabled={editing !== null}
                  placeholder={t('skills.slugPlaceholder')}
                />
              </Field>
            </div>
            <Field>
              <FieldLabel>{t('skills.name')}</FieldLabel>
              <Input
                value={draft.name}
                onChange={(e) => setDraft({ ...draft, name: e.target.value })}
                placeholder={t('skills.namePlaceholder')}
              />
            </Field>
            <Field>
              <FieldLabel>{t('skills.description')}</FieldLabel>
              <Input
                value={draft.description}
                onChange={(e) => setDraft({ ...draft, description: e.target.value })}
                placeholder={t('skills.descriptionPlaceholder')}
              />
            </Field>
            <Field>
              <FieldLabel>{t('skills.instructions')}</FieldLabel>
              <Textarea
                value={draft.instructions}
                onChange={(e) => setDraft({ ...draft, instructions: e.target.value })}
                rows={6}
                placeholder={t('skills.instructionsPlaceholder')}
              />
            </Field>
            <div className="flex items-center gap-2">
              <Button type="submit" size="sm">
                {editing ? (
                  <>
                    <SendIcon data-icon="inline-start" className="size-4" />
                    {t('skills.publishNewVersion')}
                  </>
                ) : (
                  <>
                    <PlusIcon data-icon="inline-start" className="size-4" />
                    {t('skills.create')}
                  </>
                )}
              </Button>
              {editing && (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => {
                    setEditing(null)
                    setDraft(emptyDraft)
                  }}
                >
                  {t('skills.cancel')}
                </Button>
              )}
            </div>
          </form>
        </CardContent>
      </Card>

      {error && <p className="text-sm text-destructive">{error}</p>}

      {loading ? (
        <p className="text-sm text-muted-foreground">{t('common.loading')}</p>
      ) : items.length === 0 ? (
        <Card>
          <CardContent className="pt-6 text-sm text-muted-foreground">
            {t('skills.empty')}
          </CardContent>
        </Card>
      ) : (
        items.map((item) => (
          <Card key={item.id}>
            <CardHeader>
              <div className="flex items-start justify-between gap-4">
                <div className="flex items-center gap-2">
                  <BookOpenIcon className="size-4 text-muted-foreground" />
                  <CardTitle className="text-base">{item.name}</CardTitle>
                </div>
                <div className="flex shrink-0 items-center gap-2">
                  <Badge variant="secondary">{item.domainName}</Badge>
                  <Badge variant="outline">{t('skills.version', { version: item.version })}</Badge>
                </div>
              </div>
              <CardDescription className="line-clamp-2">{item.description}</CardDescription>
            </CardHeader>
            <CardContent className="flex items-center justify-between gap-4">
              <p className="font-mono text-xs text-muted-foreground">{item.slug}</p>
              <div className="flex shrink-0 items-center gap-2">
                <Button variant="outline" size="sm" onClick={() => void startEdit(item)}>
                  {t('skills.editAndPublish')}
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={() => void remove(item)}
                  aria-label={t('skills.deleteAria', { slug: item.slug })}
                >
                  <TrashIcon data-icon="inline-start" className="size-4" />
                  {t('common.delete')}
                </Button>
              </div>
            </CardContent>
          </Card>
        ))
      )}
    </div>
  )
}
