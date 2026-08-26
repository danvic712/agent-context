import type { SkillFileInfo } from '@/lib/api'

export type FileTreeFolder = {
  kind: 'folder'
  name: string
  path: string
  children: FileTreeNode[]
}

export type FileTreeFile = {
  kind: 'file'
  name: string
  path: string
  info: SkillFileInfo
}

export type FileTreeNode = FileTreeFolder | FileTreeFile
