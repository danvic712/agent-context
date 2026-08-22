import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AlertTriangleIcon,
  CheckIcon,
  FlaskConicalIcon,
  KeyRoundIcon,
  Link2Icon,
  PlusIcon,
  RouteIcon,
  ServerIcon,
  SparklesIcon,
  Trash2Icon,
} from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import type {
  InferenceCapability,
  InferenceConfiguration,
  InferenceConfigurationInput,
  InferenceProviderInput,
  InferenceRouteInput,
  InferenceValidationCheck,
  InferenceValidationResult,
} from '@/lib/api'

export interface InferenceDraft extends InferenceConfigurationInput {
  configuredProviderIds: string[]
}

export function toInferenceInput(draft: InferenceDraft): InferenceConfigurationInput {
  const { configuredProviderIds: _configuredProviderIds, ...input } = draft
  return input
}

const id = () => crypto.randomUUID()

export function createInferenceDraft(configuration?: InferenceConfiguration): InferenceDraft {
  const configuredProviders = configuration?.providers ?? []
  const configuredRoutes = configuration?.routes ?? []
  const defaultProviders: InferenceProviderInput[] = [
    {
      id: id(),
      name: 'OpenAI',
      providerType: 'openai-compatible',
      baseUrl: 'https://api.openai.com/v1',
      apiKey: '',
    },
    {
      id: id(),
      name: 'DeepSeek',
      providerType: 'openai-compatible',
      baseUrl: 'https://api.deepseek.com/v1',
      apiKey: '',
    },
  ]
  const providers: InferenceProviderInput[] = configuredProviders.length
    ? configuredProviders.map((provider) => ({
        id: provider.id,
        name: provider.name,
        providerType: provider.providerType,
        baseUrl: provider.baseUrl,
        apiKey: '',
      }))
    : defaultProviders
  const providerId = providers.find((provider) => provider.name.trim().toLowerCase() === 'openai')?.id ?? providers[0].id

  const routeFor = (capability: InferenceCapability): InferenceRouteInput => {
    const route = configuredRoutes.find((item) => item.capability === capability)
    return {
      id: route?.id ?? id(),
      capability,
      providerId: route?.providerId ?? providerId,
      model: route?.model ?? '',
    }
  }

  return {
    name: configuration?.name ?? 'Platform default',
    providers,
    routes: [routeFor('Chat'), routeFor('Embedding')],
    configuredProviderIds: configuredProviders
      .filter((provider) => provider.apiKeyConfigured)
      .map((provider) => provider.id) ?? [],
  }
}

interface InferenceConfigFormProps {
  draft: InferenceDraft
  onChange: (draft: InferenceDraft) => void
  validation: InferenceValidationResult | null
  validating: boolean
  onValidate: () => void
  onSave?: () => void
  onReset?: () => void
  saving?: boolean
  saveDisabled?: boolean
  compact?: boolean
}

const routeIcon = (capability: InferenceCapability) =>
  capability === 'Chat' ? <SparklesIcon /> : <RouteIcon />

export function InferenceConfigForm({
  draft,
  onChange,
  validation,
  validating,
  onValidate,
  onSave,
  onReset,
  saving = false,
  saveDisabled = false,
  compact = false,
}: InferenceConfigFormProps) {
  const { t } = useTranslation()
  const [selectedProviderId, setSelectedProviderId] = useState(draft.providers[0]?.id ?? '')

  useEffect(() => {
    if (!draft.providers.some((provider) => provider.id === selectedProviderId)) {
      setSelectedProviderId(draft.providers[0]?.id ?? '')
    }
  }, [draft.providers, selectedProviderId])

  const updateProvider = (providerId: string, patch: Partial<InferenceProviderInput>) => {
    onChange({
      ...draft,
      providers: draft.providers.map((provider) =>
        provider.id === providerId ? { ...provider, ...patch } : provider,
      ),
    })
  }

  const updateRoute = (capability: InferenceCapability, patch: Partial<InferenceRouteInput>) => {
    onChange({
      ...draft,
      routes: draft.routes.map((route) =>
        route.capability === capability ? { ...route, ...patch } : route,
      ),
    })
  }

  const addProvider = () => {
    const providerId = id()
    onChange({
      ...draft,
      providers: [
        ...draft.providers,
        {
          id: providerId,
          name: '',
          providerType: 'openai-compatible',
          baseUrl: '',
          apiKey: '',
        },
      ],
    })
    setSelectedProviderId(providerId)
  }

  const providerFor = (providerId: string) => draft.providers.find((provider) => provider.id === providerId)
  const usedBy = (providerId: string) =>
    draft.routes.filter((route) => route.providerId === providerId).map((route) => route.capability)

  const removeProvider = (providerId: string) => {
    if (draft.providers.length === 1 || usedBy(providerId).length > 0) return
    const replacement = draft.providers.find((provider) => provider.id !== providerId)?.id ?? ''
    onChange({
      ...draft,
      providers: draft.providers.filter((provider) => provider.id !== providerId),
      configuredProviderIds: draft.configuredProviderIds.filter((configuredId) => configuredId !== providerId),
    })
    setSelectedProviderId(replacement)
  }

  const routeLabel = (capability: InferenceCapability) =>
    capability === 'Chat' ? t('inference.chatRoute') : t('inference.embeddingRoute')
  const routeFor = (capability: InferenceCapability) =>
    draft.routes.find((route) => route.capability === capability)
  const routeReady = (capability: InferenceCapability) => {
    const route = routeFor(capability)
    const provider = route ? providerFor(route.providerId) : undefined
    return Boolean(route?.model.trim() && provider?.name.trim() && /^https?:\/\//i.test(provider.baseUrl.trim()))
  }
  const checkFor = (capability: InferenceCapability): InferenceValidationCheck | undefined =>
    validation?.checks.find((check) => check.capability === capability)
  const targetDescription = (capability: InferenceCapability) => {
    const route = routeFor(capability)
    const provider = route ? providerFor(route.providerId) : undefined
    if (!provider) return t('inference.missingProvider')
    return `${provider.name || t('inference.provider')} · ${route?.model || t('inference.missingModel')}`
  }
  const testPassed = validation?.valid === true
  const allTargetsReady = routeReady('Chat') && routeReady('Embedding')

  const selectedProvider = providerFor(selectedProviderId)

  return (
    <div className="c-layout">
      <aside className="c-readiness">
        <div className="c-readiness__icon"><FlaskConicalIcon size={21} /></div>
        <h2 className="c-readiness__title">{t('inference.readinessTitle')}</h2>
        <p className="c-readiness__description">{t('inference.readinessDescription')}</p>
        <div className="c-checks">
          {(['Chat', 'Embedding'] as const).map((capability) => {
            const ready = routeReady(capability)
            return (
              <div key={capability} className={`c-check ${ready ? 'c-check--ok' : ''}`}>
                {ready ? <CheckIcon className="c-check__icon" /> : <AlertTriangleIcon className="c-check__icon" />}
                <div>
                  <div className="c-check__label">{t('inference.targetSelected', { target: routeLabel(capability) })}</div>
                  <div className="c-check__detail">{targetDescription(capability)}</div>
                </div>
              </div>
            )
          })}
          <div className={`c-check ${testPassed ? 'c-check--ok' : ''}`}>
            {testPassed ? <CheckIcon className="c-check__icon" /> : <AlertTriangleIcon className="c-check__icon" />}
            <div>
              <div className="c-check__label">{t('inference.endpointsVerified')}</div>
              <div className="c-check__detail">
                {testPassed ? t('inference.verifiedDetail') : t('inference.verifyBeforeSave')}
              </div>
            </div>
          </div>
        </div>
      </aside>

      <div className="c-stack">
        {!compact && (
          <div className="c-identity">
            <div>
              <div className="c-identity__title">{t('inference.configurationName')}</div>
              <div className="c-identity__note">{t('inference.configurationNameDescription')}</div>
            </div>
            <label className="c-field" htmlFor="inference-name">
              <span className="c-field__label">{t('inference.configurationName')}</span>
              <Input
                id="inference-name"
                className="c-input"
                value={draft.name}
                onChange={(event) => onChange({ ...draft, name: event.target.value })}
                placeholder={t('inference.configurationNamePlaceholder')}
              />
            </label>
          </div>
        )}

        <section className="c-panel">
          <div className="c-panel__header">
            <div>
              <div className="c-panel__title"><RouteIcon /> {t('inference.routesTitle')}</div>
              <p className="c-panel__description">{t('inference.routesDescription')}</p>
            </div>
            <span className={`c-status-badge ${allTargetsReady ? 'c-status-badge--ready' : ''}`}>
              <span className={`c-dot ${allTargetsReady ? 'c-dot--ok' : ''}`} />
              {allTargetsReady ? t('inference.routesReady') : t('inference.routesNeedSetup')}
            </span>
          </div>
          <div className="c-panel__body">
            <div className="c-route-grid">
              {(['Chat', 'Embedding'] as const).map((capability) => {
                const route = routeFor(capability)
                if (!route) return null
                const provider = providerFor(route.providerId)
                return (
                  <article key={capability} className="c-route">
                    <div className="c-route__top">
                      <div>
                        <div className="c-route__name">{routeIcon(capability)} {routeLabel(capability)}</div>
                        <div className="c-route__subtitle">{provider?.name || t('inference.selectProvider')}</div>
                      </div>
                      <span className="c-role-badge">{t('inference.required')}</span>
                    </div>
                    <div className="c-field-grid">
                      <label className="c-field" htmlFor={`route-provider-${capability}`}>
                        <span className="c-field__label">{t('inference.provider')}</span>
                        <select
                          id={`route-provider-${capability}`}
                          className="c-select"
                          value={route.providerId}
                          onChange={(event) => updateRoute(capability, { providerId: event.target.value })}
                        >
                          {draft.providers.map((item, index) => (
                            <option key={item.id} value={item.id}>
                              {item.name || t('inference.providerNumber', { number: index + 1 })}
                            </option>
                          ))}
                        </select>
                      </label>
                      <label className="c-field" htmlFor={`route-model-${capability}`}>
                        <span className="c-field__label">{t('inference.model')}</span>
                        <Input
                          id={`route-model-${capability}`}
                          className="c-input"
                          value={route.model}
                          onChange={(event) => updateRoute(capability, { model: event.target.value })}
                          placeholder={capability === 'Chat' ? t('inference.chatModelPlaceholder') : t('inference.embeddingModelPlaceholder')}
                        />
                      </label>
                    </div>
                    <div className="c-endpoint">
                      <Link2Icon /> {provider?.baseUrl || t('inference.missingEndpoint')}
                    </div>
                  </article>
                )
              })}
            </div>
          </div>
        </section>

        <section className="c-panel">
          <div className="c-panel__header">
            <div>
              <div className="c-panel__title"><ServerIcon /> {t('inference.providersTitle')}</div>
              <p className="c-panel__description">{t('inference.providersDescription')}</p>
            </div>
            <Button type="button" variant="outline" className="c-button c-button--secondary" onClick={addProvider}>
              <PlusIcon /> {t('inference.addProvider')}
            </Button>
          </div>
          <div className="c-panel__body c-provider-body">
            <div className="c-provider-layout">
              <div className="c-provider-list">
                {draft.providers.map((provider) => (
                  <button
                    key={provider.id}
                    type="button"
                    className={`c-provider-row ${provider.id === selectedProviderId ? 'c-provider-row--active' : ''}`}
                    aria-pressed={provider.id === selectedProviderId}
                    onClick={() => setSelectedProviderId(provider.id)}
                  >
                    <span className="c-provider-mark"><ServerIcon size={14} /></span>
                    <span className="c-provider-meta">
                      <span className="c-provider-name">{provider.name || t('inference.provider')}</span>
                      <span className="c-provider-url">{provider.providerType} · {provider.baseUrl || t('inference.missingEndpoint')}</span>
                      <span className="c-usage-tags">
                        {usedBy(provider.id).map((capability) => (
                          <span key={capability} className="c-usage-tag">{capability === 'Chat' ? 'Chat' : 'Embedding'}</span>
                        ))}
                      </span>
                    </span>
                  </button>
                ))}
              </div>
              <div className="c-provider-editor">
                {selectedProvider ? (
                  <>
                    <div className="c-editor-heading">
                      <div>
                        <div className="c-editor-title">{selectedProvider.name || t('inference.provider')} · {t('inference.connectionDetails')}</div>
                        <div className="c-editor-note">{t('inference.providerEditorNote')}</div>
                      </div>
                      <Button
                        type="button"
                        variant="ghost"
                        className="c-button c-button--danger"
                        disabled={draft.providers.length === 1 || usedBy(selectedProvider.id).length > 0}
                        onClick={() => removeProvider(selectedProvider.id)}
                      >
                        <Trash2Icon /> {t('inference.removeProvider')}
                      </Button>
                    </div>
                    <label className="c-field" htmlFor={`provider-name-${selectedProvider.id}`}>
                      <span className="c-field__label">{t('inference.providerName')}</span>
                      <Input
                        id={`provider-name-${selectedProvider.id}`}
                        className="c-input"
                        value={selectedProvider.name}
                        onChange={(event) => updateProvider(selectedProvider.id, { name: event.target.value })}
                        placeholder={t('inference.providerNamePlaceholder')}
                      />
                    </label>
                    <label className="c-field" htmlFor={`provider-type-${selectedProvider.id}`}>
                      <span className="c-field__label">{t('inference.providerType')}</span>
                      <Input id={`provider-type-${selectedProvider.id}`} className="c-input font-mono" value={selectedProvider.providerType} readOnly />
                    </label>
                    <label className="c-field" htmlFor={`provider-url-${selectedProvider.id}`}>
                      <span className="c-field__label">{t('inference.baseUrl')}</span>
                      <Input
                        id={`provider-url-${selectedProvider.id}`}
                        className="c-input font-mono"
                        value={selectedProvider.baseUrl}
                        onChange={(event) => updateProvider(selectedProvider.id, { baseUrl: event.target.value })}
                        placeholder={t('inference.baseUrlPlaceholder')}
                        inputMode="url"
                      />
                    </label>
                    <label className="c-field" htmlFor={`provider-key-${selectedProvider.id}`}>
                      <span className="c-field__label"><KeyRoundIcon size={13} /> {t('inference.apiKey')}</span>
                      <Input
                        id={`provider-key-${selectedProvider.id}`}
                        className="c-input font-mono"
                        type="password"
                        value={selectedProvider.apiKey}
                        onChange={(event) => updateProvider(selectedProvider.id, { apiKey: event.target.value })}
                        placeholder={selectedProvider.apiKey === '' && draft.configuredProviderIds.includes(selectedProvider.id) ? t('inference.apiKeyRetain') : t('inference.apiKeyPlaceholder')}
                        autoComplete="off"
                      />
                    </label>
                    <div className="c-endpoint"><Link2Icon /> {t('inference.routeTableNote')}</div>
                  </>
                ) : (
                  <div className="c-editor-note">{t('inference.selectProvider')}</div>
                )}
              </div>
            </div>
          </div>
        </section>

        <section className="c-panel c-panel--verify">
          <div className="c-panel__header">
            <div>
              <div className="c-panel__title"><FlaskConicalIcon /> {t('inference.verifyDraftTitle')}</div>
              <p className="c-panel__description">{t('inference.verifyDraftDescription')}</p>
            </div>
            <span className="c-status-badge">{t('inference.openAiCompatible')}</span>
          </div>
          <div className="c-panel__body c-verify-body">
            {(['Chat', 'Embedding'] as const).map((capability) => {
              const check = checkFor(capability)
              const passed = check?.valid === true
              return (
                <div key={capability} className={`c-verify-row ${passed ? 'c-verify-row--pass' : ''}`}>
                  {passed ? <CheckIcon /> : <RouteIcon />}
                  <span className="c-verify-row__label">{routeLabel(capability)}</span>
                  <span className="c-verify-code">{passed ? 'PASS' : check ? 'FAIL' : 'READY'}</span>
                </div>
              )
            })}
            <div className="flex flex-wrap items-center gap-2">
              <Button type="button" variant="outline" className="c-button c-button--secondary" onClick={onValidate} disabled={validating}>
                <FlaskConicalIcon /> {validating ? t('inference.verifying') : testPassed ? t('inference.verificationPassed') : t('inference.verifyConfiguration')}
              </Button>
              {testPassed && <span className="c-verify-message">{t('inference.verifiedDetail')}</span>}
            </div>
            {validation && !validation.valid && (
              <Alert variant="destructive" className="c-validation-alert">
                <AlertTitle>{t('inference.validationFailed')}</AlertTitle>
                <AlertDescription>
                  {validation.checks.filter((check) => !check.valid).map((check) => (
                    <p key={check.capability}>{check.message}</p>
                  ))}
                </AlertDescription>
              </Alert>
            )}
          </div>
        </section>

        {onSave && (
          <div className="c-actions">
            <div className="c-action-status"><span className="c-action-status__dot" />{t('inference.settingsActionHint')}</div>
            <div className="c-action-buttons">
              {onReset && <Button type="button" variant="ghost" className="c-button c-button--ghost" onClick={onReset}>
                {t('inference.resetDraft')}
              </Button>}
              <Button type="button" className="c-button c-button--primary" onClick={onSave} disabled={saving || saveDisabled}>
                {saving ? t('inference.saving') : t('inference.saveConfiguration')}
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
