import { useEffect, useRef } from 'react'
import * as monaco from 'monaco-editor/esm/vs/editor/editor.api.js'
import EditorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker'
import CssWorker from 'monaco-editor/esm/vs/language/css/css.worker?worker'
import HtmlWorker from 'monaco-editor/esm/vs/language/html/html.worker?worker'
import JsonWorker from 'monaco-editor/esm/vs/language/json/json.worker?worker'
import TsWorker from 'monaco-editor/esm/vs/language/typescript/ts.worker?worker'
import { useTheme } from '@/theme'

export interface MonacoSkillEditorProps {
  path: string
  value: string
  disabled?: boolean
  onChange: (value: string) => void
}

const languageByExtension: Record<string, string> = {
  bash: 'shell',
  cjs: 'javascript',
  css: 'css',
  go: 'go',
  h: 'cpp',
  hpp: 'cpp',
  html: 'html',
  java: 'java',
  js: 'javascript',
  json: 'json',
  jsx: 'javascript',
  md: 'markdown',
  markdown: 'markdown',
  mjs: 'javascript',
  ps1: 'powershell',
  py: 'python',
  sh: 'shell',
  sql: 'sql',
  ts: 'typescript',
  tsx: 'typescript',
  xml: 'xml',
  yaml: 'yaml',
  yml: 'yaml',
}

const languageForPath = (path: string) => {
  const extension = path.toLowerCase().split('.').pop()
  return extension ? languageByExtension[extension] ?? 'plaintext' : 'plaintext'
}

const workerForLabel = (label: string): Worker => {
  switch (label) {
    case 'json':
      return new JsonWorker()
    case 'css':
    case 'scss':
    case 'less':
      return new CssWorker()
    case 'html':
    case 'handlebars':
    case 'razor':
      return new HtmlWorker()
    case 'typescript':
    case 'javascript':
      return new TsWorker()
    default:
      return new EditorWorker()
  }
}

const configureWorkers = () => {
  const environment = globalThis as typeof globalThis & {
    MonacoEnvironment?: { getWorker: (_workerId: string, label: string) => Worker }
  }
  environment.MonacoEnvironment = { getWorker: (_workerId, label) => workerForLabel(label) }
}

const cssVariable = (name: string) => getComputedStyle(document.documentElement).getPropertyValue(name).trim()

const applyTheme = (resolved: 'light' | 'dark') => {
  const themeName = `agent-context-${resolved}`
  monaco.editor.defineTheme(themeName, {
    base: resolved === 'dark' ? 'vs-dark' : 'vs',
    inherit: true,
    rules: [],
    colors: {
      'editor.background': cssVariable('--code-bg'),
      'editor.foreground': cssVariable('--code-ink'),
      'editorGutter.background': cssVariable('--code-bg'),
      'editorLineNumber.foreground': cssVariable('--dim'),
      'editorLineNumber.activeForeground': cssVariable('--hi'),
      'editor.selectionBackground': cssVariable('--accent-soft'),
      'editorCursor.foreground': cssVariable('--accent'),
      'editorIndentGuide.background': cssVariable('--line'),
      'editorIndentGuide.activeBackground': cssVariable('--line2'),
      'editorWidget.background': cssVariable('--card-paper'),
      'editorWidget.border': cssVariable('--line2'),
    },
  })
  monaco.editor.setTheme(themeName)
}

export function MonacoSkillEditor({ path, value, disabled = false, onChange }: MonacoSkillEditorProps) {
  const { resolved } = useTheme()
  const containerRef = useRef<HTMLDivElement>(null)
  const editorRef = useRef<monaco.editor.IStandaloneCodeEditor | null>(null)
  const valueRef = useRef(value)
  const onChangeRef = useRef(onChange)

  valueRef.current = value
  onChangeRef.current = onChange

  useEffect(() => {
    configureWorkers()
    applyTheme(resolved)
  }, [resolved])

  useEffect(() => {
    const container = containerRef.current
    if (!container) return

    configureWorkers()
    applyTheme(resolved)
    const model = monaco.editor.createModel(
      valueRef.current,
      languageForPath(path),
      monaco.Uri.parse(`skill://editor/${encodeURIComponent(path)}`),
    )
    const editor = monaco.editor.create(container, {
      automaticLayout: true,
      contextmenu: true,
      cursorBlinking: 'smooth',
      fontFamily: 'var(--font-mono)',
      fontSize: 13,
      lineHeight: 22,
      minimap: { enabled: false },
      model,
      padding: { top: 16, bottom: 16 },
      readOnly: disabled,
      scrollBeyondLastLine: false,
      smoothScrolling: true,
      tabSize: 2,
      wordWrap: languageForPath(path) === 'markdown' ? 'on' : 'off',
    })
    const subscription = editor.onDidChangeModelContent(() => {
      onChangeRef.current(editor.getValue())
    })
    editorRef.current = editor

    return () => {
      subscription.dispose()
      editor.dispose()
      model.dispose()
      editorRef.current = null
    }
  }, [disabled, path, resolved])

  useEffect(() => {
    const editor = editorRef.current
    if (!editor || editor.getValue() === value) return
    const position = editor.getPosition()
    editor.setValue(value)
    if (position) editor.setPosition(position)
  }, [value])

  return (
    <div
      ref={containerRef}
      className="skill-monaco-editor h-[min(58vh,680px)] min-h-[360px] w-full overflow-hidden rounded-xl border border-border/70"
      data-editor="monaco"
      data-language={languageForPath(path)}
      data-readonly={disabled || undefined}
    />
  )
}
