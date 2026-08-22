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

const languageLabels: Record<string, string> = {
  cpp: 'C/C++',
  css: 'CSS',
  go: 'Go',
  html: 'HTML',
  java: 'Java',
  javascript: 'JavaScript',
  json: 'JSON',
  markdown: 'Markdown',
  powershell: 'PowerShell',
  plaintext: 'Plain text',
  python: 'Python',
  shell: 'Shell',
  sql: 'SQL',
  typescript: 'TypeScript',
  xml: 'XML',
  yaml: 'YAML',
}

export function languageForSkillPath(path: string): string {
  const extension = path.toLowerCase().split('.').pop()
  return extension ? languageByExtension[extension] ?? 'plaintext' : 'plaintext'
}

export function languageLabelForSkillPath(path: string): string {
  const language = languageForSkillPath(path)
  return languageLabels[language] ?? language
}
