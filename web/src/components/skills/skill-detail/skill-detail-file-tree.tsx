import { ChevronDownIcon, FileIcon, FolderIcon } from 'lucide-react'
import type { FileTreeNode } from './types'
import { useTranslation } from 'react-i18next'

interface SkillDetailFileTreeProps {
  nodes: FileTreeNode[]
  selectedPath: string
  onSelect: (path: string) => void
}

function FileTreeNodeView({ node, selectedPath, onSelect }: { node: FileTreeNode; selectedPath: string; onSelect: (path: string) => void }) {
  const { t } = useTranslation()

  if (node.kind === 'folder') {
    return (
      <details className="skill-file-tree__folder" open>
        <summary className="skill-file-tree__folder-row" role="treeitem">
          <ChevronDownIcon className="skill-file-tree__chevron size-3.5" aria-hidden="true" />
          <FolderIcon className="size-3.5 shrink-0 text-[var(--hi)]" aria-hidden="true" />
          <span className="skill-file-tree__name" title={node.path}>{node.name}</span>
        </summary>
        <div className="skill-file-tree__children" role="group">
          {node.children.map((child) => (
            <FileTreeNodeView key={child.path} node={child} selectedPath={selectedPath} onSelect={onSelect} />
          ))}
        </div>
      </details>
    )
  }

  const selected = selectedPath === node.path
  return (
    <button
      type="button"
      className="skill-file-tree__file-row"
      data-selected={selected || undefined}
      onClick={() => onSelect(node.path)}
      role="treeitem"
      aria-selected={selected}
      title={node.path}
    >
      <FileIcon className="size-3.5 shrink-0" aria-hidden="true" />
      <span className="skill-file-tree__name">{node.name}</span>
      {node.info.binary && <span className="skill-file-tree__binary">{t('skills.binaryShort')}</span>}
    </button>
  )
}

export function SkillDetailFileTree({ nodes, selectedPath, onSelect }: SkillDetailFileTreeProps) {
  const { t } = useTranslation()

  return (
    <div className="skill-file-tree" role="tree" aria-label={t('skills.packageFilesTitle')}>
      {nodes.length > 0 ? nodes.map((node) => (
        <FileTreeNodeView key={node.path} node={node} selectedPath={selectedPath} onSelect={onSelect} />
      )) : (
        <p className="skill-detail-empty">{t('skills.emptyPackage')}</p>
      )}
    </div>
  )
}
