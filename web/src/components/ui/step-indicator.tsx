import { CheckIcon } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

export interface StepIndicatorItem {
  id: string
  label: string
}

interface StepIndicatorProps {
  steps: readonly StepIndicatorItem[]
  currentId: string
  completedIds?: readonly string[]
  ariaLabel: string
  onSelect?: (id: string) => void
  isSelectable?: (id: string) => boolean
  disabled?: boolean
  className?: string
}

export function StepIndicator({
  steps,
  currentId,
  completedIds = [],
  ariaLabel,
  onSelect,
  isSelectable,
  disabled = false,
  className,
}: StepIndicatorProps) {
  return (
    <nav className={cn('ui-step-indicator', className)} aria-label={ariaLabel}>
      <ol className="ui-step-indicator__list">
        {steps.map((step, index) => {
          const current = step.id === currentId
          const complete = completedIds.includes(step.id)
          const selectable = Boolean(onSelect) && (step.id === currentId || (isSelectable ? isSelectable(step.id) : true))

          return (
            <li key={step.id} className="ui-step-indicator__item" data-current={current} data-complete={complete}>
              <Button
                type="button"
                variant="ghost"
                className="ui-step-indicator__step"
                aria-current={current ? 'step' : undefined}
                disabled={disabled || !selectable}
                onClick={() => onSelect?.(step.id)}
              >
                <span className="ui-step-indicator__number" aria-hidden="true">
                  {complete ? <CheckIcon /> : index + 1}
                </span>
                <span className="ui-step-indicator__label">{step.label}</span>
              </Button>
            </li>
          )
        })}
      </ol>
    </nav>
  )
}
