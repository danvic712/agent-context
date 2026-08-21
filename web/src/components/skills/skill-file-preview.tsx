import { MarkdownView } from '@/components/markdown-view'
import { CodePreview } from '@/components/code-preview'

interface SkillFilePreviewProps {
  path: string
  content: string
}

const isMarkdown = (path: string) => path.toLowerCase().endsWith('.md')

export function SkillFilePreview({ path, content }: SkillFilePreviewProps) {
  if (isMarkdown(path)) {
    return (
      <div data-preview-type="markdown">
        <MarkdownView content={content} />
      </div>
    )
  }

  return <CodePreview path={path} content={content} />
}
