import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { AlertTriangleIcon, ArrowLeftIcon, CheckCircle2Icon, CheckIcon, SparklesIcon, UserRoundIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ActionBar, ActionBarStatus } from '@/components/ui/action-bar'
import { Button } from '@/components/ui/button'
import { SectionHeading, Surface } from '@/components/ui/surface'
import type { InferenceDraft } from '@/components/inference-config-form'
import type { AccountForm } from '../types'

interface ReviewStepProps {
  account: AccountForm
  draft: InferenceDraft
  error: string | null
  language: string
  serviceReady: boolean
  inferenceSkipped: boolean
  submitting: boolean
  onBack: () => void
  onFinish: () => void
}

export function ReviewStep({
  account,
  draft,
  error,
  language,
  serviceReady,
  inferenceSkipped,
  submitting,
  onBack,
  onFinish,
}: ReviewStepProps) {
  const { t } = useTranslation()

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    void onFinish()
  }

  return (
    <form className="setup-step-form" onSubmit={submit}>
      <div className="setup-review-layout">
        <Surface as="section" className="setup-review-surface" aria-labelledby="setup-review-title">
          <div className="setup-surface-header">
            <SectionHeading
              titleId="setup-review-title"
              title={t('wizard.reviewTitle')}
              description={t(inferenceSkipped ? 'wizard.reviewSkippedDescription' : 'wizard.reviewDescription')}
            />
          </div>
          <div className="setup-surface-body setup-review-body">
            <div className="setup-review-overview">
              <div className="setup-review-item"><span>{t('wizard.displayName')}</span><strong>{account.displayName}</strong></div>
              <div className="setup-review-item"><span>{t('wizard.language')}</span><strong>{language}</strong></div>
              <div className="setup-review-item"><span>{t('inference.providersTitle')}</span><strong>{inferenceSkipped ? t('wizard.notConfigured') : t('wizard.providerCount', { count: draft.providers.length })}</strong></div>
            </div>

            <div className="setup-review-block">
              <div className="setup-review-block__heading"><UserRoundIcon aria-hidden="true" /><div><h3>{t('wizard.accountSummary')}</h3><p>{t('wizard.reviewAccountDescription')}</p></div></div>
              <div className="setup-review-details">
                <div><span>{t('wizard.displayName')}</span><strong>{account.displayName}</strong></div>
                <div><span>{t('wizard.email')}</span><strong>{account.email}</strong></div>
                <div><span>{t('wizard.language')}</span><strong>{language}</strong></div>
              </div>
            </div>

            <div className="setup-review-block">
              <div className="setup-review-block__heading">
                <SparklesIcon aria-hidden="true" />
                <div>
                  <h3>{t('wizard.inferenceSummary')}</h3>
                  <p>{t(inferenceSkipped ? 'wizard.reviewInferenceSkippedDescription' : 'wizard.reviewInferenceDescription')}</p>
                </div>
                <span className={`setup-section-badge ${inferenceSkipped ? 'setup-section-badge--warning' : 'setup-section-badge--success'}`}>
                  {inferenceSkipped ? <AlertTriangleIcon aria-hidden="true" /> : <CheckIcon aria-hidden="true" />}
                  {t(inferenceSkipped ? 'wizard.notConfiguredBeforeCreate' : 'wizard.verifiedBeforeCreate')}
                </span>
              </div>
              <div className="setup-review-details">
                {inferenceSkipped ? (
                  <div><span>{t('wizard.inferenceSummary')}</span><strong>{t('wizard.configureLater')}</strong></div>
                ) : draft.routes.map((route) => {
                    const provider = draft.providers.find((item) => item.id === route.providerId)
                    return <div key={route.id}><span>{route.capability === 'Chat' ? t('inference.chatRoute') : t('inference.embeddingRoute')}</span><strong>{provider?.name || t('inference.provider')} · {route.model}</strong></div>
                  })}
              </div>
            </div>
          </div>
        </Surface>

        <Surface as="aside" tone="muted" className="setup-assistant setup-review-assistant">
          <div className={`setup-assistant__icon ${inferenceSkipped ? 'setup-assistant__icon--warning' : 'setup-assistant__icon--success'}`}>
            {inferenceSkipped ? <AlertTriangleIcon aria-hidden="true" /> : <CheckCircle2Icon aria-hidden="true" />}
          </div>
          <SectionHeading title={t('wizard.reviewAsideTitle')} description={t(inferenceSkipped ? 'wizard.skipForNowDescription' : 'wizard.atomicCreateHint')} className="setup-assistant__heading" />
          <div className="setup-check-list">
            <div className="setup-check-item setup-check-item--success"><CheckIcon aria-hidden="true" /><span><strong>{t('wizard.accountSummary')}</strong><small>{account.email}</small></span></div>
            <div className={`setup-check-item ${inferenceSkipped ? 'setup-check-item--warning' : 'setup-check-item--success'}`}>
              {inferenceSkipped ? <AlertTriangleIcon aria-hidden="true" /> : <CheckIcon aria-hidden="true" />}
              <span><strong>{t('wizard.inferenceSummary')}</strong><small>{t(inferenceSkipped ? 'wizard.notConfiguredBeforeCreate' : 'wizard.verifiedBeforeCreate')}</small></span>
            </div>
          </div>
        </Surface>
      </div>

      {inferenceSkipped && (
        <Alert className="setup-skip-alert">
          <AlertTriangleIcon aria-hidden="true" />
          <AlertTitle>{t('wizard.skipForNow')}</AlertTitle>
          <AlertDescription>{t('wizard.skipForNowDescription')}</AlertDescription>
        </Alert>
      )}
      {error && <Alert variant="destructive" className="setup-alert"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
      <ActionBar sticky className="setup-action-bar" status={<ActionBarStatus>{t(inferenceSkipped ? 'wizard.reviewSkippedActionHint' : 'wizard.reviewActionHint')}</ActionBarStatus>}>
        <Button type="button" variant="ghost" onClick={onBack} disabled={submitting}><ArrowLeftIcon />{t('wizard.back')}</Button>
        <Button type="submit" size="lg" disabled={submitting || !serviceReady}>{submitting ? t('wizard.settingUp') : t('wizard.createWorkspace')}</Button>
      </ActionBar>
    </form>
  )
}
