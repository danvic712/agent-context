import { PlusIcon, Trash2Icon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import type {
  InferenceCapability,
  InferenceConfiguration,
  InferenceConfigurationInput,
  InferenceProviderInput,
  InferenceRouteInput,
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
  const providerId = configuration?.providers[0]?.id ?? id()
  const providers: InferenceProviderInput[] = configuration?.providers.length
    ? configuration.providers.map((provider) => ({
        id: provider.id,
        name: provider.name,
        providerType: provider.providerType,
        baseUrl: provider.baseUrl,
        apiKey: '',
      }))
    : [
        {
          id: providerId,
          name: '',
          providerType: 'openai-compatible',
          baseUrl: '',
          apiKey: '',
        },
      ]

  const routeFor = (capability: InferenceCapability): InferenceRouteInput => {
    const route = configuration?.routes.find((item) => item.capability === capability)
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
    configuredProviderIds: configuration?.providers
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
  saving?: boolean
  saveDisabled?: boolean
  compact?: boolean
}

export function InferenceConfigForm({
  draft,
  onChange,
  validation,
  validating,
  onValidate,
  onSave,
  saving = false,
  saveDisabled = false,
  compact = false,
}: InferenceConfigFormProps) {
  const { t } = useTranslation()

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
    onChange({
      ...draft,
      providers: [
        ...draft.providers,
        {
          id: id(),
          name: '',
          providerType: 'openai-compatible',
          baseUrl: '',
          apiKey: '',
        },
      ],
      configuredProviderIds: draft.configuredProviderIds,
    })
  }

  const removeProvider = (providerId: string) => {
    if (draft.providers.length === 1) return
    const replacement = draft.providers.find((provider) => provider.id !== providerId)?.id
    onChange({
      ...draft,
      providers: draft.providers.filter((provider) => provider.id !== providerId),
      routes: draft.routes.map((route) =>
        route.providerId === providerId && replacement ? { ...route, providerId: replacement } : route,
      ),
      configuredProviderIds: draft.configuredProviderIds.filter((id) => id !== providerId),
    })
  }

  const routeLabel = (capability: InferenceCapability) =>
    capability === 'Chat' ? t('inference.chatRoute') : t('inference.embeddingRoute')

  return (
    <div className="flex flex-col gap-5">
      {!compact && (
        <Field>
          <FieldLabel htmlFor="inference-name">{t('inference.configurationName')}</FieldLabel>
          <FieldContent>
            <Input
              id="inference-name"
              value={draft.name}
              onChange={(event) => onChange({ ...draft, name: event.target.value })}
              placeholder={t('inference.configurationNamePlaceholder')}
            />
          </FieldContent>
        </Field>
      )}

      <div>
        <div className="mb-3 flex items-start justify-between gap-3">
          <div>
            <h3 className="text-sm font-medium">{t('inference.providersTitle')}</h3>
            <p className="text-sm text-muted-foreground">{t('inference.providersDescription')}</p>
          </div>
          <Button type="button" size="sm" variant="outline" onClick={addProvider}>
            <PlusIcon className="size-4" />
            {t('inference.addProvider')}
          </Button>
        </div>

        <div className="grid gap-3 lg:grid-cols-2">
          {draft.providers.map((provider, index) => (
            <Card key={provider.id} className="bg-background/70 shadow-none">
              <CardHeader className="pb-3">
                <CardTitle className="flex items-center justify-between text-sm">
                  <span>{t('inference.providerNumber', { number: index + 1 })}</span>
                  <div className="flex items-center gap-2">
                    <Badge variant="outline">{t('inference.openAiCompatible')}</Badge>
                    <Button
                      type="button"
                      size="icon-xs"
                      variant="ghost"
                      onClick={() => removeProvider(provider.id)}
                      disabled={draft.providers.length === 1}
                      aria-label={t('inference.removeProvider')}
                    >
                      <Trash2Icon />
                    </Button>
                  </div>
                </CardTitle>
              </CardHeader>
              <CardContent>
                <FieldGroup>
                  <Field>
                    <FieldLabel htmlFor={`provider-name-${provider.id}`}>{t('inference.providerName')}</FieldLabel>
                    <FieldContent>
                      <Input
                        id={`provider-name-${provider.id}`}
                        value={provider.name}
                        onChange={(event) => updateProvider(provider.id, { name: event.target.value })}
                        placeholder={t('inference.providerNamePlaceholder')}
                      />
                    </FieldContent>
                  </Field>
                  <Field>
                    <FieldLabel htmlFor={`provider-url-${provider.id}`}>{t('inference.baseUrl')}</FieldLabel>
                    <FieldContent>
                      <Input
                        id={`provider-url-${provider.id}`}
                        value={provider.baseUrl}
                        onChange={(event) => updateProvider(provider.id, { baseUrl: event.target.value })}
                        placeholder={t('inference.baseUrlPlaceholder')}
                        inputMode="url"
                      />
                    </FieldContent>
                  </Field>
                  <Field>
                    <FieldLabel htmlFor={`provider-key-${provider.id}`}>{t('inference.apiKey')}</FieldLabel>
                    <FieldContent>
                      <Input
                        id={`provider-key-${provider.id}`}
                        type="password"
                        value={provider.apiKey}
                        onChange={(event) => updateProvider(provider.id, { apiKey: event.target.value })}
                        placeholder={t('inference.apiKeyPlaceholder')}
                        autoComplete="off"
                      />
                      {provider.apiKey === '' && draft.configuredProviderIds.includes(provider.id) && (
                        <p className="text-xs text-muted-foreground">{t('inference.apiKeyRetain')}</p>
                      )}
                    </FieldContent>
                  </Field>
                </FieldGroup>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>

      <div>
        <div className="mb-3">
          <h3 className="text-sm font-medium">{t('inference.routesTitle')}</h3>
          <p className="text-sm text-muted-foreground">{t('inference.routesDescription')}</p>
        </div>
        <div className="grid gap-3 lg:grid-cols-2">
          {(['Chat', 'Embedding'] as const).map((capability) => {
            const route = draft.routes.find((item) => item.capability === capability)!
            return (
              <Card key={capability} className="bg-background/70 shadow-none">
                <CardHeader className="pb-3">
                  <CardTitle className="text-sm">{routeLabel(capability)}</CardTitle>
                </CardHeader>
                <CardContent className="grid gap-3">
                  <Field>
                    <FieldLabel htmlFor={`route-provider-${capability}`}>{t('inference.provider')}</FieldLabel>
                    <FieldContent>
                      <select
                        id={`route-provider-${capability}`}
                        value={route.providerId}
                        onChange={(event) => updateRoute(capability, { providerId: event.target.value })}
                        className="flex h-9 w-full items-center rounded-lg border border-input bg-transparent px-3 text-sm shadow-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                      >
                        {draft.providers.map((provider, index) => (
                          <option key={provider.id} value={provider.id}>
                            {provider.name || t('inference.providerNumber', { number: index + 1 })}
                          </option>
                        ))}
                      </select>
                    </FieldContent>
                  </Field>
                  <Field>
                    <FieldLabel htmlFor={`route-model-${capability}`}>{t('inference.model')}</FieldLabel>
                    <FieldContent>
                      <Input
                        id={`route-model-${capability}`}
                        value={route.model}
                        onChange={(event) => updateRoute(capability, { model: event.target.value })}
                        placeholder={capability === 'Chat' ? t('inference.chatModelPlaceholder') : t('inference.embeddingModelPlaceholder')}
                      />
                    </FieldContent>
                  </Field>
                </CardContent>
              </Card>
            )
          })}
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3 border-t pt-4">
        <Button type="button" onClick={onValidate} disabled={validating}>
          {validating ? t('inference.verifying') : t('inference.verifyConfiguration')}
        </Button>
        {validation && (
          <div className="flex flex-wrap gap-2" aria-live="polite">
            {validation.checks.map((check) => (
              <Badge key={check.capability} variant={check.valid ? 'default' : 'destructive'}>
                {routeLabel(check.capability)}: {check.valid ? t('inference.verified') : t('inference.failed')}
              </Badge>
            ))}
          </div>
        )}
        {onSave && (
          <Button type="button" variant="secondary" onClick={onSave} disabled={saving || saveDisabled}>
            {saving ? t('inference.saving') : t('inference.saveConfiguration')}
          </Button>
        )}
      </div>
      {validation && !validation.valid && (
        <Alert variant="destructive">
          <AlertTitle>{t('inference.validationFailed')}</AlertTitle>
          <AlertDescription>
            {validation.checks.filter((check) => !check.valid).map((check) => (
              <p key={check.capability}>{check.message}</p>
            ))}
          </AlertDescription>
        </Alert>
      )}
    </div>
  )
}
