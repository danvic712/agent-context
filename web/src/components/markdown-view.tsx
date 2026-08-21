import Markdown from 'react-markdown'
import { CodePreview } from '@/components/code-preview'

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
              return <CodePreview language={match[1]} content={code} />
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
