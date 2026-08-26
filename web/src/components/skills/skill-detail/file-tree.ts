import type { SkillFileInfo } from '@/lib/api'
import type { FileTreeFolder, FileTreeNode } from './types'

export const fileName = (path: string) => path.split('/').pop() ?? path

export const sortFiles = (files: SkillFileInfo[]) => [...files].sort((a, b) =>
  fileName(a.path).localeCompare(fileName(b.path), undefined, { numeric: true, sensitivity: 'base' })
    || a.path.localeCompare(b.path, undefined, { numeric: true, sensitivity: 'base' }),
)

const isFolder = (node: FileTreeNode): node is FileTreeFolder => node.kind === 'folder'

const sortTreeNodes = (nodes: FileTreeNode[]) => [...nodes].sort((a, b) => {
  if (a.kind !== b.kind) return a.kind === 'folder' ? -1 : 1
  return a.name.localeCompare(b.name, undefined, { numeric: true, sensitivity: 'base' })
})

export const buildFileTree = (files: SkillFileInfo[], folders: string[] = []): FileTreeNode[] => {
  const root: FileTreeFolder = { kind: 'folder', name: '', path: '', children: [] }

  const ensureFolder = (parts: string[]) => {
    let parent = root
    for (let index = 0; index < parts.length; index += 1) {
      const path = parts.slice(0, index + 1).join('/')
      let folder = parent.children.find(
        (node): node is FileTreeFolder => isFolder(node) && node.path === path,
      )
      if (!folder) {
        folder = { kind: 'folder', name: parts[index], path, children: [] }
        parent.children.push(folder)
      }
      parent = folder
    }
    return parent
  }

  folders.forEach((path) => ensureFolder(path.split('/').filter(Boolean)))

  for (const info of files) {
    const parts = info.path.split('/').filter(Boolean)
    if (parts.length === 0) continue

    const parent = ensureFolder(parts.slice(0, -1))
    parent.children.push({
      kind: 'file',
      name: parts[parts.length - 1],
      path: info.path,
      info,
    })
  }

  const sortBranch = (branch: FileTreeFolder) => {
    branch.children = sortTreeNodes(branch.children)
    branch.children.filter(isFolder).forEach(sortBranch)
  }
  sortBranch(root)
  return root.children
}
