import { RefreshCwIcon, UploadIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { ActionBar } from '@/components/ui/action-bar'

interface SkillActionBarProps {
  count: number
  refreshing?: boolean
  onUpload: () => void
  onRefresh: () => void
}

export function SkillActionBar({ count, refreshing = false, onUpload, onRefresh }: SkillActionBarProps) {
  const { t } = useTranslation()

  return (
    <ActionBar
      className="mb-5"
      status={(
        <>
          <span className="size-2 rounded-full bg-ok shadow-[0_0_0_4px_color-mix(in_srgb,var(--ok)_14%,transparent)]" />
          <span>{t('skills.visibleCount', { count })}</span>
        </>
      )}
    >
        <Button type="button" size="sm" variant="ghost" onClick={onRefresh} disabled={refreshing}>
          <RefreshCwIcon data-icon="inline-start" className={refreshing ? 'size-3.5 animate-spin' : 'size-3.5'} />
          {t('skills.refresh')}
        </Button>
        <Button type="button" size="sm" onClick={onUpload}>
          <UploadIcon data-icon="inline-start" className="size-3.5" />
          {t('skills.uploadSkill')}
        </Button>
    </ActionBar>
  )
}
