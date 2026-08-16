import { useEffect, useState } from 'react'
import { SettingsIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Field, FieldContent, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { getLlmOptions, saveLlmOptions, type LlmOptionsDto } from '@/lib/api'

export function SettingsPage() {
  const [options, setOptions] = useState<LlmOptionsDto | null>(null)
  const [baseUrl, setBaseUrl] = useState('')
  const [apiKey, setApiKey] = useState('')
  const [model, setModel] = useState('')
  const [embeddingModel, setEmbeddingModel] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [saved, setSaved] = useState(false)

  const load = async () => {
    setLoading(true)
    setError(null)
    try {
      const current = await getLlmOptions()
      setOptions(current)
      setBaseUrl(current.baseUrl ?? '')
      setModel(current.model ?? '')
      setEmbeddingModel(current.embeddingModel ?? '')
      // The API key is never returned — the field stays blank, a masked badge shows it's set.
      setApiKey('')
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Failed to load settings')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const save = async () => {
    setError(null)
    setSaved(false)
    try {
      const result = await saveLlmOptions({
        baseUrl: baseUrl.trim(),
        apiKey: apiKey.trim(),
        model: model.trim(),
        embeddingModel: embeddingModel.trim() || null,
      })
      setOptions(result)
      setApiKey('')
      setSaved(true)
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : 'Failed to save settings')
    }
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <SettingsIcon className="size-4 text-muted-foreground" />
            LLM endpoint
          </CardTitle>
          <CardDescription>
            The OpenAI-compatible endpoint the Learning Engine uses for extraction and
            embedding (ADR 0003). Changes apply immediately — no restart needed.
          </CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          {loading ? (
            <p className="text-sm text-muted-foreground">Loading…</p>
          ) : (
            <>
              <div className="flex items-center gap-2">
                {options?.configured ? (
                  <Badge variant="default">configured</Badge>
                ) : (
                  <Badge variant="outline">not configured — engine idles</Badge>
                )}
                {options?.configured && options.maskedApiKey && (
                  <Badge variant="secondary">key {options.maskedApiKey}</Badge>
                )}
              </div>

              <Field>
                <FieldLabel htmlFor="settings-base-url">Base URL</FieldLabel>
                <FieldContent>
                  <Input
                    id="settings-base-url"
                    value={baseUrl}
                    onChange={(e) => setBaseUrl(e.target.value)}
                    placeholder="https://api.openai.com/v1"
                  />
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel htmlFor="settings-api-key">API key</FieldLabel>
                <FieldContent>
                  <Input
                    id="settings-api-key"
                    type="password"
                    value={apiKey}
                    onChange={(e) => setApiKey(e.target.value)}
                    placeholder={options?.configured ? '•••••• (unchanged if left blank)' : 'sk-…'}
                    autoComplete="off"
                  />
                  {options?.configured && apiKey === '' && (
                    <p className="text-xs text-muted-foreground">
                      Leaving it blank keeps the existing key.
                    </p>
                  )}
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel htmlFor="settings-model">Model</FieldLabel>
                <FieldContent>
                  <Input
                    id="settings-model"
                    value={model}
                    onChange={(e) => setModel(e.target.value)}
                    placeholder="gpt-4o-mini"
                  />
                </FieldContent>
              </Field>
              <Field>
                <FieldLabel htmlFor="settings-embedding-model">Embedding model (optional)</FieldLabel>
                <FieldContent>
                  <Input
                    id="settings-embedding-model"
                    value={embeddingModel}
                    onChange={(e) => setEmbeddingModel(e.target.value)}
                    placeholder="text-embedding-3-small"
                  />
                </FieldContent>
              </Field>

              <div className="flex items-center gap-3">
                <Button size="sm" onClick={() => void save()}>
                  Save
                </Button>
                {saved && <p className="text-sm text-muted-foreground">Saved ✓</p>}
              </div>

              {error && (
                <Alert variant="destructive">
                  <AlertTitle>Save failed</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  )
}
