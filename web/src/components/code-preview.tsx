import { useEffect, useState } from 'react'
import { codeToHtml } from 'shiki'

interface CodePreviewProps {
  content: string
  language?: string
  path?: string
}

const languageByExtension: Record<string, string> = {
  bash: 'shellscript',
  cjs: 'javascript',
  css: 'css',
  go: 'go',
  h: 'c',
  hpp: 'cpp',
  html: 'html',
  java: 'java',
  js: 'javascript',
  json: 'json',
  jsx: 'jsx',
  md: 'markdown',
  mjs: 'javascript',
  ps1: 'powershell',
  py: 'python',
  sh: 'shellscript',
  sql: 'sql',
  ts: 'typescript',
  tsx: 'tsx',
  xml: 'xml',
  yaml: 'yaml',
  yml: 'yaml',
}

const languageForPath = (path: string): string => {
  const extension = path.toLowerCase().split('.').pop()
  return extension ? languageByExtension[extension] ?? 'text' : 'text'
}

export function CodePreview({ content, language, path }: CodePreviewProps) {
  const resolvedLanguage = language ?? (path ? languageForPath(path) : 'text')
  const [html, setHtml] = useState('')

  useEffect(() => {
    let cancelled = false
    setHtml('')
    void codeToHtml(content, {
      lang: resolvedLanguage,
      theme: 'github-dark',
    })
      .then((result) => {
        if (!cancelled) setHtml(result)
      })
      .catch(() => {
        if (!cancelled) setHtml('')
      })
    return () => {
      cancelled = true
    }
  }, [content, resolvedLanguage])

  if (!html) {
    return (
      <pre
        className="skill-code-preview overflow-x-auto rounded-xl border border-border/70 bg-[var(--code-bg)] p-4 font-mono text-xs leading-6 text-[var(--code-ink)]"
        data-preview-type="code"
      >
        <code>{content}</code>
      </pre>
    )
  }

  return (
    <div
      className="skill-code-preview overflow-x-auto rounded-xl border border-border/70 bg-[var(--code-bg)] p-1"
      data-preview-type="code"
      dangerouslySetInnerHTML={{ __html: html }}
    />
  )
}
