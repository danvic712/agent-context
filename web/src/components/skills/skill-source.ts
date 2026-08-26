import type { SkillSourceType } from '@/lib/api'

export const skillSourceKey = (sourceType: SkillSourceType | null): string => {
  if (sourceType === 'manual') return 'skills.sourceManual'
  if (sourceType === 'file') return 'skills.sourceFile'
  if (sourceType === 'zip') return 'skills.sourceZip'
  if (sourceType === 'skills_sh') return 'skills.sourceSkillsSh'
  if (sourceType === 'local_copy') return 'skills.sourceLocalCopy'
  return 'skills.sourceUnknown'
}
