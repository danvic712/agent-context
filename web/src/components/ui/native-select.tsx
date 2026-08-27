import type { SelectHTMLAttributes } from 'react'
import { ChevronsUpDownIcon } from 'lucide-react'
import { cn } from '@/lib/utils'

interface NativeSelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  wrapperClassName?: string
  iconClassName?: string
}

export function NativeSelect({ className, wrapperClassName, iconClassName, children, ...props }: NativeSelectProps) {
  return (
    <span className={cn('ui-select', wrapperClassName)}>
      <select className={cn('ui-select__control', className)} {...props}>
        {children}
      </select>
      <ChevronsUpDownIcon className={cn('ui-select__icon', iconClassName)} aria-hidden="true" />
    </span>
  )
}
