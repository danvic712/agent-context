import { useState } from 'react'
import { ArrowLeftIcon, ArrowRightIcon, SparklesIcon } from 'lucide-react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@/components/ui/card'
import { Field, FieldContent, FieldGroup, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { postSetup, saveLlmOptions } from '@/lib/api'

interface FirstRunWizardProps {
  onComplete: () => void
}

interface AccountForm {
  displayName: string
  email: string
  password: string
}

interface LlmForm {
  baseUrl: string
  apiKey: string
  model: string
  embeddingModel: string
}

const emptyAccount: AccountForm = { displayName: '', email: '', password: '' }
const emptyLlm: LlmForm = { baseUrl: '', apiKey: '', model: '', embeddingModel: '' }

export function FirstRunWizard({ onComplete }: FirstRunWizardProps) {
  const [step, setStep] = useState<1 | 2>(1)
  const [account, setAccount] = useState<AccountForm>(emptyAccount)
  const [llm, setLlm] = useState<LlmForm>(emptyLlm)
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function finish(configureLlm: boolean) {
    setError(null)
    setSubmitting(true)
    try {
      await postSetup(account.displayName.trim(), account.email.trim(), account.password)
      if (configureLlm) {
        await saveLlmOptions({
          baseUrl: llm.baseUrl.trim(),
          apiKey: llm.apiKey.trim(),
          model: llm.model.trim(),
          embeddingModel: llm.embeddingModel.trim() || null,
        })
      }
      onComplete()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Setup failed. Please try again.')
    } finally {
      setSubmitting(false)
    }
  }

  function submitAccount(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (!account.displayName.trim() || !account.email.trim() || account.password.length < 8) {
      setError('Please fill in every field; the password needs at least 8 characters.')
      return
    }

    setStep(2)
  }

  function submitLlm(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)
    void finish(true)
  }

  return (
    <div className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <SparklesIcon data-icon="inline-start" />
            Welcome to Agent Context
          </CardTitle>
          <CardDescription>
            {step === 1
              ? 'A shared context layer for your AI agents. Start by creating your admin account and personal workspace.'
              : 'Optional: point the Learning Engine at your LLM endpoint. You can configure this later in Settings.'}
          </CardDescription>
        </CardHeader>

        {step === 1 ? (
          <form onSubmit={submitAccount}>
            <CardContent>
              <FieldGroup>
                <Field>
                  <FieldLabel htmlFor="display-name">Display name</FieldLabel>
                  <FieldContent>
                    <Input
                      id="display-name"
                      value={account.displayName}
                      onChange={(event) => setAccount({ ...account, displayName: event.target.value })}
                      autoComplete="name"
                      placeholder="Ada Lovelace"
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="email">Email</FieldLabel>
                  <FieldContent>
                    <Input
                      id="email"
                      type="email"
                      value={account.email}
                      onChange={(event) => setAccount({ ...account, email: event.target.value })}
                      autoComplete="email"
                      placeholder="ada@example.com"
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="password">Password</FieldLabel>
                  <FieldContent>
                    <Input
                      id="password"
                      type="password"
                      value={account.password}
                      onChange={(event) => setAccount({ ...account, password: event.target.value })}
                      autoComplete="new-password"
                      placeholder="At least 8 characters"
                    />
                  </FieldContent>
                </Field>
              </FieldGroup>
              {error && (
                <Alert variant="destructive" className="mt-4">
                  <AlertTitle>Setup failed</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </CardContent>
            <CardFooter>
              <Button type="submit" className="w-full">
                Continue
                <ArrowRightIcon data-icon="inline-end" className="size-4" />
              </Button>
            </CardFooter>
          </form>
        ) : (
          <form onSubmit={submitLlm}>
            <CardContent>
              <FieldGroup>
                <Field>
                  <FieldLabel htmlFor="llm-base-url">Base URL</FieldLabel>
                  <FieldContent>
                    <Input
                      id="llm-base-url"
                      value={llm.baseUrl}
                      onChange={(event) => setLlm({ ...llm, baseUrl: event.target.value })}
                      placeholder="https://api.openai.com/v1"
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="llm-api-key">API key</FieldLabel>
                  <FieldContent>
                    <Input
                      id="llm-api-key"
                      type="password"
                      value={llm.apiKey}
                      onChange={(event) => setLlm({ ...llm, apiKey: event.target.value })}
                      placeholder="sk-…"
                      autoComplete="off"
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="llm-model">Model</FieldLabel>
                  <FieldContent>
                    <Input
                      id="llm-model"
                      value={llm.model}
                      onChange={(event) => setLlm({ ...llm, model: event.target.value })}
                      placeholder="gpt-4o-mini"
                    />
                  </FieldContent>
                </Field>
                <Field>
                  <FieldLabel htmlFor="llm-embedding-model">Embedding model (optional)</FieldLabel>
                  <FieldContent>
                    <Input
                      id="llm-embedding-model"
                      value={llm.embeddingModel}
                      onChange={(event) => setLlm({ ...llm, embeddingModel: event.target.value })}
                      placeholder="text-embedding-3-small"
                    />
                  </FieldContent>
                </Field>
              </FieldGroup>

              <Alert className="mt-4">
                <AlertTitle>Skip for now?</AlertTitle>
                <AlertDescription>
                  Without an LLM endpoint the Learning Engine stays idle and won't
                  distill Knowledge until you configure one in Settings.
                </AlertDescription>
              </Alert>

              {error && (
                <Alert variant="destructive" className="mt-4">
                  <AlertTitle>Setup failed</AlertTitle>
                  <AlertDescription>{error}</AlertDescription>
                </Alert>
              )}
            </CardContent>
            <CardFooter className="flex gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => setStep(1)}
                disabled={submitting}
              >
                <ArrowLeftIcon data-icon="inline-start" className="size-4" />
                Back
              </Button>
              <Button
                type="button"
                variant="ghost"
                onClick={() => void finish(false)}
                disabled={submitting}
              >
                Skip
              </Button>
              <Button type="submit" disabled={submitting} className="flex-1">
                {submitting ? 'Setting up…' : 'Create my workspace'}
              </Button>
            </CardFooter>
          </form>
        )}
      </Card>
    </div>
  )
}
