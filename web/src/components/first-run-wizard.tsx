import { useState } from 'react'
import { SparklesIcon } from 'lucide-react'
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
import { postSetup } from '@/lib/api'

interface FirstRunWizardProps {
  onComplete: () => void
}

export function FirstRunWizard({ onComplete }: FirstRunWizardProps) {
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [submitting, setSubmitting] = useState(false)

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError(null)

    if (!displayName.trim() || !email.trim() || password.length < 8) {
      setError('Please fill in every field; the password needs at least 8 characters.')
      return
    }

    setSubmitting(true)
    try {
      await postSetup(displayName.trim(), email.trim(), password)
      onComplete()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Setup failed. Please try again.')
    } finally {
      setSubmitting(false)
    }
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
            A shared context layer for your AI agents. Start by creating your admin
            account and personal workspace.
          </CardDescription>
        </CardHeader>
        <form onSubmit={handleSubmit}>
          <CardContent>
            <FieldGroup>
              <Field>
                <FieldLabel htmlFor="display-name">Display name</FieldLabel>
                <FieldContent>
                  <Input
                    id="display-name"
                    value={displayName}
                    onChange={(event) => setDisplayName(event.target.value)}
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
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
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
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
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
            <Button type="submit" disabled={submitting} className="w-full">
              Create my workspace
            </Button>
          </CardFooter>
        </form>
      </Card>
    </div>
  )
}
