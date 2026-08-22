const languageByExtension: Record<string, string> = {
  bash: 'shell',
  c: 'c',
  cjs: 'javascript',
  css: 'css',
  cs: 'csharp',
  cpp: 'cpp',
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
  kt: 'kotlin',
  lua: 'lua',
  php: 'php',
  pl: 'perl',
  rb: 'ruby',
  rs: 'rust',
  sh: 'shell',
  sql: 'sql',
  swift: 'swift',
  ts: 'typescript',
  tsx: 'typescript',
  xml: 'xml',
  yaml: 'yaml',
  yml: 'yaml',
}

const languageLabels: Record<string, string> = {
  c: 'C',
  cpp: 'C/C++',
  css: 'CSS',
  csharp: 'C#',
  go: 'Go',
  html: 'HTML',
  java: 'Java',
  javascript: 'JavaScript',
  json: 'JSON',
  markdown: 'Markdown',
  powershell: 'PowerShell',
  perl: 'Perl',
  plaintext: 'Plain text',
  python: 'Python',
  kotlin: 'Kotlin',
  lua: 'Lua',
  php: 'PHP',
  ruby: 'Ruby',
  rust: 'Rust',
  shell: 'Shell',
  sql: 'SQL',
  swift: 'Swift',
  typescript: 'TypeScript',
  xml: 'XML',
  yaml: 'YAML',
}

const languageByShebang: Record<string, string> = {
  bash: 'shell',
  bun: 'javascript',
  deno: 'javascript',
  fish: 'shell',
  node: 'javascript',
  nodejs: 'javascript',
  perl: 'perl',
  php: 'php',
  powershell: 'powershell',
  pwsh: 'powershell',
  python: 'python',
  python2: 'python',
  python3: 'python',
  ruby: 'ruby',
  sh: 'shell',
  zsh: 'shell',
}

const languageFromShebang = (content: string): string | undefined => {
  const firstLine = content.split(/\r?\n/, 1)[0]?.trim()
  if (!firstLine?.startsWith('#!')) return undefined

  const commandParts = firstLine.slice(2).trim().split(/\s+/)
  const executable = commandParts[0]?.endsWith('/env')
    ? commandParts.slice(1).find((part) => !part.startsWith('-'))
    : commandParts[0]
  const command = executable?.split('/').pop()?.toLowerCase()
  return command ? languageByShebang[command] : undefined
}

const languageFromContent = (content: string): string | undefined => {
  const trimmed = content.trim()
  if (!trimmed) return undefined

  if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
    try {
      JSON.parse(trimmed)
      return 'json'
    } catch {
      // Continue with the other heuristics for incomplete drafts.
    }
  }

  if (/^<!doctype\s+html\b/i.test(trimmed) || /^<html(?:\s|>)/i.test(trimmed)) return 'html'
  if (/^<\?xml(?:\s|>)/i.test(trimmed)) return 'xml'
  if (/^\s*(?:SELECT|INSERT|UPDATE|DELETE|CREATE\s+TABLE|ALTER\s+TABLE|DROP\s+TABLE)\b/i.test(trimmed)) return 'sql'
  if (/^\s*(?:set\s+-[eux]|export\s+[A-Z_][A-Z0-9_]*=|if\s+\[|echo\s+)/m.test(content)) return 'shell'
  if (/^\s*(?:def|class)\s+\w+\s*\(|^\s*(?:from|import)\s+[\w.]+/m.test(content)) return 'python'
  if (/^\s*(?:interface|type)\s+\w+\s*[={]|:\s*(?:string|number|boolean|unknown|never)\b/m.test(content)) return 'typescript'
  if (/^\s*(?:import|export)\s+|\b(?:const|let|var|function)\s+\w+/m.test(content)) return 'javascript'
  if (/^\s*[.#]?[\w-]+\s*\{[^}]*\b(?:color|display|margin|padding|font-size)\s*:/ms.test(content)) return 'css'
  if (/^\s*[\w.-]+:\s+\S+/m.test(content) && !/[{};]/.test(content)) return 'yaml'
  if (/^#{1,6}\s+|\*\*[^*]+\*\*|^```/m.test(content)) return 'markdown'

  return undefined
}

export function languageForSkillPath(path: string, content = ''): string {
  const extension = path.toLowerCase().split('.').pop()
  return (extension && languageByExtension[extension])
    ?? languageFromShebang(content)
    ?? languageFromContent(content)
    ?? 'plaintext'
}

export function languageLabelForSkillPath(path: string, content = ''): string {
  const language = languageForSkillPath(path, content)
  return languageLabels[language] ?? language
}
