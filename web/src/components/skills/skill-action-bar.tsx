import { PlusIcon, RefreshCwIcon, UploadIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'

interface SkillActionBarProps {
  count: number
  refreshing?: boolean
  onCreate: () => void
  onUpload: () => void
  onRefresh: () => void
}

export function SkillActionBar({ count, refreshing = false, onCreate, onUpload, onRefresh }: SkillActionBarProps) {
  const { t } = useTranslation()

  return (
    <div className="mb-5 flex flex-col gap-3 rounded-2xl border border-border/80 bg-card/80 p-3 shadow-sm backdrop-blur sm:flex-row sm:items-center sm:justify-between">
      <div className="flex items-center gap-2 text-xs text-muted-foreground">
        <span className="size-2 rounded-full bg-ok shadow-[0_0_0_4px_color-mix(in_srgb,var(--ok)_14%,transparent)]" />
        <span>{t('skills.visibleCount', { count })}</span>
      </div>
      <div className="flex flex-wrap gap-2">
        <Button type="button" size="sm" variant="ghost" onClick={onRefresh} disabled={refreshing}>
          <RefreshCwIcon data-icon="inline-start" className={refreshing ? 'size-3.5 animate-spin' : 'size-3.5'} />
          {t('skills.refresh')}
        </Button>
        <Button type="button" size="sm" variant="outline" onClick={onCreate}>
          <PlusIcon data-icon="inline-start" className="size-3.5" />
          {t('skills.createSkill')}
        </Button>
        <Button type="button" size="sm" onClick={onUpload}>
          <UploadIcon data-icon="inline-start" className="size-3.5" />
          {t('skills.uploadSkill')}
        </Button>
      </div>
    </div>
  )
}
