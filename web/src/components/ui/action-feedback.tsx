import { createContext, useCallback, useContext, useState, type ReactNode } from 'react'
import { CheckCircle2Icon, CircleAlertIcon, InfoIcon, XIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { cn } from '@/lib/utils'

type FeedbackTone = 'success' | 'error' | 'info'

interface FeedbackItem {
  id: number
  message: string
  tone: FeedbackTone
}

interface ActionFeedbackContextValue {
  push: (message: string, tone?: FeedbackTone) => void
}

const ActionFeedbackContext = createContext<ActionFeedbackContextValue | null>(null)
let nextFeedbackId = 0

export function ActionFeedbackProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<FeedbackItem[]>([])

  const dismiss = useCallback((id: number) => {
    setItems((current) => current.filter((item) => item.id !== id))
  }, [])

  const push = useCallback((message: string, tone: FeedbackTone = 'info') => {
    const id = ++nextFeedbackId
    setItems((current) => [...current.slice(-2), { id, message, tone }])
    window.setTimeout(() => dismiss(id), 4200)
  }, [dismiss])

  return (
    <ActionFeedbackContext.Provider value={{ push }}>
      {children}
      <ActionFeedbackViewport items={items} onDismiss={dismiss} />
    </ActionFeedbackContext.Provider>
  )
}

export function useActionFeedback() {
  const context = useContext(ActionFeedbackContext)
  if (!context) throw new Error('useActionFeedback must be used inside ActionFeedbackProvider')
  return context
}

function ActionFeedbackViewport({
  items,
  onDismiss,
}: {
  items: FeedbackItem[]
  onDismiss: (id: number) => void
}) {
  const { t } = useTranslation()

  return (
    <div className="ui-feedback-stack" aria-live="polite" aria-atomic="true">
      {items.map((item) => {
        const Icon = item.tone === 'success' ? CheckCircle2Icon : item.tone === 'error' ? CircleAlertIcon : InfoIcon
        return (
          <div key={item.id} className={cn('ui-feedback', `ui-feedback--${item.tone}`)} role={item.tone === 'error' ? 'alert' : 'status'}>
            <Icon className="size-4 shrink-0" aria-hidden="true" />
            <span className="min-w-0 flex-1">{item.message}</span>
            <button type="button" className="ui-feedback__dismiss" onClick={() => onDismiss(item.id)} aria-label={t('appShell.dismissFeedback')}>
              <XIcon className="size-3.5" aria-hidden="true" />
            </button>
          </div>
        )
      })}
    </div>
  )
}
