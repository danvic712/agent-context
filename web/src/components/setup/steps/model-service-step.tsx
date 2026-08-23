import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowLeftIcon, ArrowRightIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ActionBar, ActionBarStatus } from '@/components/ui/action-bar'
import { Button } from '@/components/ui/button'
import {
  InferenceConfigForm,
  type InferenceDraft,
} from '@/components/inference-config-form'
import type { InferenceValidationResult } from '@/lib/api'

interface ModelServiceStepProps {
  draft: InferenceDraft
  error: string | null
  validating: boolean
  validation: InferenceValidationResult | null
  onBack: () => void
  onChange: (draft: InferenceDraft) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  onValidate: () => void
}

export function ModelServiceStep({
  draft,
  error,
  validating,
  validation,
  onBack,
  onChange,
  onSubmit,
  onValidate,
}: ModelServiceStepProps) {
  const { t } = useTranslation()

  return (
    <form className="setup-step-form" onSubmit={onSubmit}>
      <InferenceConfigForm
        className="setup-inference-form"
        draft={draft}
        onChange={onChange}
        validation={validation}
        validating={validating}
        onValidate={onValidate}
        showVerifyAction={false}
        compact
      />
      {error && <Alert variant="destructive" className="setup-alert"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
      <ActionBar sticky className="setup-action-bar" status={<ActionBarStatus>{t('wizard.stepTwoActionHint')}</ActionBarStatus>}>
        <Button type="button" variant="ghost" onClick={onBack} disabled={validating}><ArrowLeftIcon />{t('wizard.back')}</Button>
        <Button type="submit" size="lg" disabled={validating}>{validating ? t('inference.verifying') : t('wizard.testAndReview')} <ArrowRightIcon /></Button>
      </ActionBar>
    </form>
  )
}
