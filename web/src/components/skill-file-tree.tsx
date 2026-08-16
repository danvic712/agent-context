import { useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  ChevronDownIcon,
  ChevronRightIcon,
  FileCode2Icon,
  FileTextIcon,
  FolderIcon,
  FolderOpenIcon,
  FolderPlusIcon,
  PencilIcon,
  PlusIcon,
  TrashIcon,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import type { SkillFileInfo } from '@/lib/api'

interface SkillFileTreeProps {
  files: SkillFileInfo[]
  activePath: string
  onSelect: (path: string) => void
  onCreateFile: (path: string) => Promise<void>
  onCreateFolder: (name: string) => Promise<void>
  onRename: (from: string, to: string) => Promise<void>
  onDelete: (path: string) => void
}

type TreeNode =
  | { kind: 'folder'; name: string; path: string; children: TreeNode[] }
  | { kind: 'file'; path: string; name: string; ext: string }

const MAIN = 'SKILL.md'

const extColor = (ext: string): string => {
  switch (ext) {
    case 'ts':
    case 'tsx':
    case 'js':
    case 'mjs':
      return 'var(--teal)'
    case 'sql':
    case 'sh':
    case 'bash':
      return 'var(--warn)'
    case 'md':
    case 'markdown':
      return 'var(--accent)'
    case 'svg':
    case 'png':
    case 'jpg':
    case 'gif':
    case 'webp':
      return 'var(--chart-4, #7a8fc9)'
    default:
      return 'var(--dim)'
  }
}

/** Builds a folder/file tree from flat manifest paths. `.gitkeep` keeps a folder
 *  visible without cluttering the tree (the folder itself is preserved). */
function buildTree(files: SkillFileInfo[]): TreeNode[] {
  interface Bucket {
    files: TreeNode[]
    sub: Map<string, Bucket>
  }
  const root: Bucket = { files: [], sub: new Map() }

  const bucket = (dirs: string[]): Bucket => {
    let current: Bucket = root
    for (const dir of dirs) {
      let entry = current.sub.get(dir)
      if (!entry) {
        entry = { files: [], sub: new Map() }
        current.sub.set(dir, entry)
      }
      current = entry
    }
    return current
  }

  for (const file of files) {
    const parts = file.path.split('/')
    const name = parts[parts.length - 1]
    const dirParts = parts.slice(0, -1)

    // A .gitkeep keeps its folder visible without adding a file node.
    if (name === '.gitkeep') {
      bucket(dirParts)
      continue
    }

    const ext = name.includes('.') ? name.split('.').pop()! : ''
    bucket(dirParts).files.push({ kind: 'file', path: file.path, name, ext })
  }

  const toNodes = (bucket: Bucket, parentPath: string): TreeNode[] => {
    const folders: TreeNode[] = []
    for (const [name, entry] of bucket.sub) {
      const path = parentPath ? `${parentPath}/${name}` : name
      folders.push({ kind: 'folder', name, path, children: toNodes(entry, path) })
    }
    folders.sort((a, b) => a.name.localeCompare(b.name))

    const filesSorted = [...bucket.files].sort((a, b) =>
      a.path === MAIN ? -1 : b.path === MAIN ? 1 : a.path.localeCompare(b.path),
    )
    return [...filesSorted, ...folders]
  }

  return toNodes(root, '')
}

/** Rejects traversal/absolute/placeholder paths — shared by add + rename. */
const validPath = (value: string) => {
  const name = value.split('/').pop() ?? ''
  return (
    value.length > 0 &&
    !value.startsWith('/') &&
    !value.includes('..') &&
    !value.includes('\\') &&
    name !== '.gitkeep'
  )
}

const dirOf = (path: string) => {
  const i = path.lastIndexOf('/')
  return i === -1 ? '' : path.slice(0, i)
}

const allFolderPaths = (nodes: TreeNode[], out: string[] = []): string[] => {
  for (const n of nodes) {
    if (n.kind === 'folder') {
      out.push(n.path)
      allFolderPaths(n.children, out)
    }
  }
  return out
}

export function SkillFileTree({
  files,
  activePath,
  onSelect,
  onCreateFile,
  onCreateFolder,
  onRename,
  onDelete,
}: SkillFileTreeProps) {
  const { t } = useTranslation()
  const [open, setOpen] = useState<Set<string>>(new Set())
  const [adding, setAdding] = useState<'file' | 'folder' | null>(null)
  const [draft, setDraft] = useState('')
  const draftRef = useRef('')
  const [renaming, setRenaming] = useState<string | null>(null)
  const [renameDraft, setRenameDraft] = useState('')
  const [invalid, setInvalid] = useState(false)

  // Default: everything expanded (empty open-set means "all open").
  const tree = useMemo(() => buildTree(files), [files])
  const allFolders = useMemo(() => allFolderPaths(tree), [tree])

  const isOpen = (path: string) => open.size === 0 || open.has(path)
  const toggle = (path: string) => {
    setOpen((prev) => {
      const next = new Set(prev.size ? prev : allFolders)
      if (next.has(path)) next.delete(path)
      else next.add(path)
      return next
    })
  }

  const beginAdd = (kind: 'file' | 'folder', into?: string) => {
    setAdding(kind)
    setDraft(into ? `${into}/` : '')
    draftRef.current = into ? `${into}/` : ''
    setInvalid(false)
    setRenaming(null)
  }

  const submitAdd = async () => {
    const value = draftRef.current.trim()
    setInvalid(false)
    if (!value) {
      setAdding(null)
      return
    }
    if (!validPath(value)) {
      setInvalid(true)
      return
    }
    setDraft('')
    draftRef.current = ''
    setAdding(null)
    if (adding === 'file') await onCreateFile(value)
    else await onCreateFolder(value)
  }

  const beginRename = (path: string) => {
    const dir = dirOf(path)
    setRenaming(path)
    setRenameDraft(dir ? `${dir}/` : '')
    setInvalid(false)
  }

  const submitRename = async (from: string) => {
    const to = renameDraft.trim()
    if (!to || to === from) {
      setRenaming(null)
      return
    }
    if (!validPath(to)) {
      setInvalid(true)
      return
    }
    setRenaming(null)
    await onRename(from, to)
  }

  const renderNode = (node: TreeNode): React.ReactNode => {
    if (node.kind === 'folder') {
      const expanded = isOpen(node.path)
      return (
        <div key={node.path} className="st-folder">
          <div
            className="st-row group/row"
            role="treeitem"
            aria-expanded={expanded}
            tabIndex={0}
            onClick={() => toggle(node.path)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault()
                toggle(node.path)
              }
            }}
          >
            <span className="st-caret">
              {expanded ? <ChevronDownIcon className="size-3" /> : <ChevronRightIcon className="size-3" />}
            </span>
            {expanded ? <FolderOpenIcon className="st-ico" /> : <FolderIcon className="st-ico" />}
            <span className="st-name">{node.name}</span>
            <span className="text-[9px] text-muted-foreground">▾ {node.children.length}</span>
            <span className="st-grow" />
            {renaming === node.path ? (
              <span className="flex" onClick={(e) => e.stopPropagation()}>
                <Input
                  autoFocus
                  value={renameDraft}
                  onChange={(e) => setRenameDraft(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') void submitRename(node.path)
                    if (e.key === 'Escape') setRenaming(null)
                  }}
                  className={cn('h-6 w-24 font-mono text-[11px]', invalid && 'border-destructive')}
                />
              </span>
            ) : (
              <>
                <span
                  className="hidden items-center gap-1 rounded px-1 hover:text-foreground group-hover/row:flex"
                  onClick={(e) => {
                    e.stopPropagation()
                    beginAdd('file', node.path)
                  }}
                  role="button"
                  aria-label={`${t('skills.newFile')} in ${node.path}`}
                  title={`${t('skills.newFile')}…`}
                >
                  <PlusIcon className="size-3" />
                </span>
                <span
                  className="hidden items-center gap-1 rounded px-1 hover:text-foreground group-hover/row:flex"
                  onClick={(e) => {
                    e.stopPropagation()
                    beginRename(node.path)
                  }}
                  role="button"
                  aria-label={t('skills.renameFile')}
                  title={t('skills.renameFile')}
                >
                  <PencilIcon className="size-3" />
                </span>
              </>
            )}
          </div>
          {expanded && <div className="st-children">{node.children.map(renderNode)}</div>}
        </div>
      )
    }

    const isActive = node.path === activePath
    return (
      <div key={node.path} className="st-file group/row">
        <div
          className={cn('st-row', isActive && 'on')}
          role="treeitem"
          tabIndex={0}
          onClick={() => onSelect(node.path)}
          onKeyDown={(e) => {
            if (e.key === 'Enter' || e.key === ' ') {
              e.preventDefault()
              onSelect(node.path)
            }
          }}
        >
          {node.path === MAIN ? (
            <FileTextIcon className="st-ico" style={{ color: 'var(--accent)' }} />
          ) : (
            <FileCode2Icon className="st-ico" style={{ color: extColor(node.ext) }} />
          )}
          <span className="st-name">{node.name}</span>
          <span className="st-ext">{node.ext}</span>
          {renaming === node.path ? (
            <Input
              autoFocus
              value={renameDraft}
              onChange={(e) => setRenameDraft(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') void submitRename(node.path)
                if (e.key === 'Escape') setRenaming(null)
              }}
              onClick={(e) => e.stopPropagation()}
              className={cn('h-6 w-28 font-mono text-[11px]', invalid && 'border-destructive')}
            />
          ) : (
            <>
              <span className="st-grow" />
              <span
                className="hidden items-center gap-1 rounded px-1 hover:text-foreground group-hover/row:flex"
                onClick={(e) => {
                  e.stopPropagation()
                  beginRename(node.path)
                }}
                role="button"
                aria-label={t('skills.renameFile')}
                title={t('skills.renameFile')}
              >
                <PencilIcon className="size-3" />
              </span>
              <span
                className="hidden items-center gap-1 rounded px-1 text-destructive group-hover/row:flex"
                onClick={(e) => {
                  e.stopPropagation()
                  onDelete(node.path)
                }}
                role="button"
                aria-label={t('common.delete')}
                title={t('common.delete')}
              >
                <TrashIcon className="size-3" />
              </span>
            </>
          )}
        </div>
      </div>
    )
  }

  return (
    <div className="skill-tree" role="tree" aria-label={t('skills.packageTree')}>
      <div className="mb-2 flex items-center justify-between px-2">
        <span className="text-[9px] font-medium uppercase tracking-[0.16em] text-muted-foreground">
          {t('skills.packageTree')}
          <span className="ml-1 text-[12px] normal-case tracking-normal" style={{ color: 'var(--teal)' }}>
            · {files.length}
          </span>
        </span>
        <span className="flex gap-1">
          <Button size="icon" variant="ghost" className="h-6 w-6" title={t('skills.newFile')} onClick={() => beginAdd('file')}>
            <PlusIcon className="size-3.5" />
          </Button>
          <Button size="icon" variant="ghost" className="h-6 w-6" title={t('skills.newFolder')} onClick={() => beginAdd('folder')}>
            <FolderPlusIcon className="size-3.5" />
          </Button>
        </span>
      </div>

      {adding && (
        <div className="mb-2 px-2">
          <Input
            autoFocus
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value)
              draftRef.current = e.target.value
              setInvalid(false)
            }}
            placeholder={adding === 'file' ? t('skills.filePath') : t('skills.folderName')}
            onKeyDown={(e) => {
              if (e.key === 'Enter') void submitAdd()
              if (e.key === 'Escape') {
                setAdding(null)
                setDraft('')
              }
            }}
            onBlur={() => void submitAdd()}
            className={cn('h-7 font-mono text-[11px]', invalid && 'border-destructive')}
          />
          {invalid && <p className="mt-1 text-[10.5px] text-destructive">{t('skills.invalidPath')}</p>}
        </div>
      )}
      {renaming && invalid && (
        <p className="px-2 pb-1 text-[10.5px] text-destructive">{t('skills.invalidPath')}</p>
      )}

      {tree.length === 0 ? (
        <p className="px-2 py-1 text-[11.5px] text-muted-foreground">{t('skills.emptyPackageTree')}</p>
      ) : (
        tree.map(renderNode)
      )}
    </div>
  )
}
