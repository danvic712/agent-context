import { useEffect, useState } from 'react'
import Markdown from 'react-markdown'
import { codeToHtml } from 'shiki'

/**
 * Renders a Skill's SKILL.md as markdown (T12). Code blocks are highlighted with
 * shiki's `codeToHtml` (lazy-loaded per language), bound to the Direction D
 * palette through the --shiki-light / --shiki-dark CSS variables.
 */
function HighlightedCode({ language, code }: { language: string; code: string }) {
  const [html, setHtml] = useState('')

  useEffect(() => {
    let cancelled = false
    codeToHtml(code, {
      lang: language || 'text',
      themes: { light: 'github-light', dark: 'github-dark' },
      defaultColor: false,
    })
      .then((result) => {
        if (!cancelled) setHtml(result)
      })
      .catch(() => {
        // Fall back to plain text when a language fails to load.
        if (!cancelled) setHtml('')
      })
    return () => {
      cancelled = true
    }
  }, [code, language])

  if (!html) {
    return <pre className="md-plain"><code>{code}</code></pre>
  }
  return <div className="md-shiki" dangerouslySetInnerHTML={{ __html: html }} />
}

export function MarkdownView({ content }: { content: string }) {
  return (
    <div className="md-view">
      <Markdown
        components={{
          code(props) {
            const { children, className } = props
            const match = /language-(\w+)/.exec(className ?? '')
            const code = String(children).replace(/\n$/, '')
            if (match) {
              return <HighlightedCode language={match[1]} code={code} />
            }
            return <code className={className}>{children}</code>
          },
          // Block code arrives wrapped in <pre><code>: our code component already
          // renders the highlighted block (pre included), so unwrap the extra <pre>.
          pre({ children }) {
            return <>{children}</>
          },
        }}
      >
        {content}
      </Markdown>
    </div>
  )
}
