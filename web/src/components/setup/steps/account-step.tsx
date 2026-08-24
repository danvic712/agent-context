import type { FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { ArrowRightIcon, LanguagesIcon, LockKeyholeIcon, SparklesIcon, UserRoundIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { ActionBar, ActionBarStatus } from '@/components/ui/action-bar'
import { Button } from '@/components/ui/button'
import { Field, FieldContent, FieldDescription, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { SectionHeading, Surface } from '@/components/ui/surface'
import type { AccountForm } from '../types'

interface AccountStepProps {
  account: AccountForm
  error: string | null
  language: string
  onAccountChange: (account: AccountForm) => void
  onLanguageChange: (locale: string) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

export function AccountStep({
  account,
  error,
  language,
  onAccountChange,
  onLanguageChange,
  onSubmit,
}: AccountStepProps) {
  const { t } = useTranslation()

  return (
    <form className="setup-step-form" onSubmit={onSubmit}>
      <div className="setup-content-grid">
        <Surface as="aside" tone="muted" className="setup-assistant">
          <div className="setup-assistant__icon"><SparklesIcon aria-hidden="true" /></div>
          <SectionHeading
            title={t('wizard.accountAsideTitle')}
            description={t('wizard.accountAsideDescription')}
            className="setup-assistant__heading"
          />
          <div className="setup-check-list">
            <div className="setup-check-item"><UserRoundIcon aria-hidden="true" />{t('wizard.accountAsideItemAccount')}</div>
            <div className="setup-check-item"><LanguagesIcon aria-hidden="true" />{t('wizard.accountAsideItemLanguage')}</div>
            <div className="setup-check-item"><LockKeyholeIcon aria-hidden="true" />{t('wizard.accountAsideItemProtected')}</div>
          </div>
        </Surface>

        <Surface as="section" className="setup-form-surface" aria-labelledby="setup-account-title">
          <div className="setup-surface-header">
            <SectionHeading
              titleId="setup-account-title"
              title={t('wizard.accountSummary')}
              description={t('wizard.stepAccountDescription')}
              aside={<span className="setup-section-badge">{t('wizard.stepOneLabel')}</span>}
            />
          </div>
          <div className="setup-surface-body">
            <div className="setup-field-grid">
              <Field className="setup-field">
                <FieldLabel htmlFor="display-name" required className="setup-field-label">{t('wizard.displayName')}</FieldLabel>
                <FieldContent>
                  <Input id="display-name" aria-required="true" value={account.displayName} onChange={(event) => onAccountChange({ ...account, displayName: event.target.value })} autoComplete="name" placeholder={t('wizard.displayNamePlaceholder')} />
                </FieldContent>
              </Field>
              <Field className="setup-field">
                <FieldLabel htmlFor="email" required className="setup-field-label">{t('wizard.email')}</FieldLabel>
                <FieldContent>
                  <Input id="email" type="email" aria-required="true" value={account.email} onChange={(event) => onAccountChange({ ...account, email: event.target.value })} autoComplete="email" placeholder={t('wizard.emailPlaceholder')} />
                </FieldContent>
              </Field>
            </div>
            <Field className="setup-field">
              <FieldLabel htmlFor="password" required className="setup-field-label">{t('wizard.password')}</FieldLabel>
              <FieldContent>
                <Input id="password" type="password" aria-required="true" value={account.password} onChange={(event) => onAccountChange({ ...account, password: event.target.value })} autoComplete="new-password" placeholder={t('wizard.passwordPlaceholder')} />
                <FieldDescription className="setup-field-description">{t('wizard.passwordHelp')}</FieldDescription>
              </FieldContent>
            </Field>
            <Field className="setup-field">
              <FieldLabel id="setup-language-label" className="setup-field-label">{t('wizard.language')}</FieldLabel>
              <FieldContent>
                <div id="setup-language" className="setup-language-options" role="group" aria-labelledby="setup-language-label">
                  {(['en-US', 'zh-CN'] as const).map((locale) => (
                    <Button key={locale} type="button" variant="ghost" aria-pressed={language === locale} className="setup-language-option" onClick={() => void onLanguageChange(locale)}>
                      {locale === 'en-US' ? t('wizard.english') : t('wizard.chinese')}
                    </Button>
                  ))}
                </div>
              </FieldContent>
            </Field>
          </div>
        </Surface>
      </div>

      {error && <Alert variant="destructive" className="setup-alert"><AlertTitle>{t('wizard.setupFailed')}</AlertTitle><AlertDescription>{error}</AlertDescription></Alert>}
      <ActionBar sticky className="setup-action-bar" status={<ActionBarStatus>{t('wizard.stepOneActionHint')}</ActionBarStatus>}>
        <Button type="submit" size="lg">{t('wizard.continue')} <ArrowRightIcon /></Button>
      </ActionBar>
    </form>
  )
}
