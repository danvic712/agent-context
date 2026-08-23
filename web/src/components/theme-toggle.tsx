import { useTranslation } from 'react-i18next'
import { MoonIcon, SunIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { useTheme } from '@/theme'
import { cn } from '@/lib/utils'

/**
 * Topbar quick toggle (T12): Light / Dark switch that persists to the DB
 * (settings.theme). When the platform theme is "system", the currently resolved
 * value is highlighted and either button switches to that explicit theme.
 */
export function ThemeToggle() {
  const { t } = useTranslation()
  const { resolved, setMode } = useTheme()

  return (
    <div
      role="group"
      aria-label={t('settings.theme')}
      className="ui-theme-toggle flex items-center gap-1 rounded-lg border border-border bg-secondary p-1"
    >
      <Button
        type="button"
        variant="ghost"
        size="sm"
        onClick={() => void setMode('light')}
        aria-label={t('settings.themeToggleLight')}
        aria-pressed={resolved === 'light'}
        className={cn('h-7 min-h-8 min-w-8 px-2.5', resolved === 'light' && 'bg-primary text-primary-foreground hover:bg-primary hover:text-primary-foreground')}
      >
        <SunIcon className="size-3.5" />
      </Button>
      <Button
        type="button"
        variant="ghost"
        size="sm"
        onClick={() => void setMode('dark')}
        aria-label={t('settings.themeToggleDark')}
        aria-pressed={resolved === 'dark'}
        className={cn('h-7 min-h-8 min-w-8 px-2.5', resolved === 'dark' && 'bg-primary text-primary-foreground hover:bg-primary hover:text-primary-foreground')}
      >
        <MoonIcon className="size-3.5" />
      </Button>
    </div>
  )
}
