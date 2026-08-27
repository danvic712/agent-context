import type { ReactNode } from 'react'
import { ListFilterIcon, RefreshCwIcon, UploadIcon, XIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { SectionHeading, Surface } from '@/components/ui/surface'
import { NativeSelect } from '@/components/ui/native-select'
import type { SkillListSort, SkillSourceType } from '@/lib/api'

export interface InstalledSkillFilters {
  search: string
  domain: string
  sourceType: SkillSourceType | ''
  sort: SkillListSort
}

interface SkillSearchFiltersProps {
  filters: InstalledSkillFilters
  refreshing?: boolean
  onChange: (filters: InstalledSkillFilters) => void
  onClear: () => void
  onRefresh: () => void
  onUpload: () => void
  children?: ReactNode
}

const sourceOptions: Array<SkillSourceType | ''> = ['', 'manual', 'file', 'zip', 'skills_sh', 'local_copy']
const sortOptions: SkillListSort[] = ['updated-desc', 'updated-asc', 'name-asc', 'name-desc', 'version-desc', 'version-asc']

const sourceLabelKey = (source: SkillSourceType): string => {
  if (source === 'skills_sh') return 'skills.sourceSkillsSh'
  if (source === 'local_copy') return 'skills.sourceLocalCopy'
  if (source === 'manual') return 'skills.sourceManual'
  if (source === 'file') return 'skills.sourceFile'
  return 'skills.sourceZip'
}

export function SkillSearchFilters({ filters, refreshing = false, onChange, onClear, onRefresh, onUpload, children }: SkillSearchFiltersProps) {
  const { t } = useTranslation()
  const activeFilterCount = [filters.search.trim(), filters.domain.trim(), filters.sourceType, filters.sort !== 'updated-desc' ? filters.sort : ''].filter(Boolean).length

  return (
    <Surface className="skill-library-filter" aria-labelledby="skill-search-filters-title">
      <div className="skill-library-filter__head">
        <div className="flex min-w-0 items-start gap-2.5">
          <div className="skill-library-filter__icon">
            <ListFilterIcon className="size-4" />
          </div>
          <SectionHeading
            titleId="skill-search-filters-title"
            title={t('skills.searchFiltersTitle')}
            description={t('skills.searchFiltersDescription')}
            className="min-w-0"
          />
        </div>
        <div className="skill-library-filter__actions">
          <Button type="button" size="sm" variant="outline" onClick={onRefresh} disabled={refreshing}>
            <RefreshCwIcon data-icon="inline-start" className={refreshing ? 'size-3.5 animate-spin' : 'size-3.5'} />
            {t('skills.refresh')}
          </Button>
          <Button type="button" size="sm" onClick={onUpload}>
            <UploadIcon data-icon="inline-start" className="size-3.5" />
            {t('skills.uploadSkill')}
          </Button>
          {activeFilterCount > 0 && (
            <Button type="button" size="sm" variant="ghost" className="skill-library-filter__clear" onClick={onClear}>
              <XIcon data-icon="inline-start" className="size-3.5" />
              {t('skills.clearFilters')}
            </Button>
          )}
        </div>
      </div>

      <div className="skill-library-filter__fields">
        <label className="sm:col-span-2 lg:col-span-2" htmlFor="skill-search-query">
          <span className="c-field__label">{t('skills.searchLabel')}</span>
          <Input
            id="skill-search-query"
            value={filters.search}
            onChange={(event) => onChange({ ...filters, search: event.target.value })}
            placeholder={t('skills.searchPlaceholder')}
            className="mt-1 h-9 text-xs"
          />
        </label>
        <label htmlFor="skill-search-domain">
          <span className="c-field__label">{t('skills.domainFilterLabel')}</span>
          <Input
            id="skill-search-domain"
            value={filters.domain}
            onChange={(event) => onChange({ ...filters, domain: event.target.value })}
            placeholder={t('skills.domainFilterPlaceholder')}
            className="mt-1 h-9 text-xs"
          />
        </label>
        <label htmlFor="skill-search-source">
          <span className="c-field__label">{t('skills.sourceFilterLabel')}</span>
          <NativeSelect
            id="skill-search-source"
            aria-label={t('skills.sourceFilterLabel')}
            className="c-select text-xs"
            wrapperClassName="mt-1"
            value={filters.sourceType}
            onChange={(event) => onChange({ ...filters, sourceType: event.target.value as SkillSourceType | '' })}
          >
            {sourceOptions.map((source) => (
              <option key={source || 'all'} value={source}>{t(source ? sourceLabelKey(source) : 'skills.allSources')}</option>
            ))}
          </NativeSelect>
        </label>
        <label htmlFor="skill-search-sort">
          <span className="c-field__label">{t('skills.sortLabel')}</span>
          <NativeSelect
            id="skill-search-sort"
            aria-label={t('skills.sortLabel')}
            className="c-select text-xs"
            wrapperClassName="mt-1"
            value={filters.sort}
            onChange={(event) => onChange({ ...filters, sort: event.target.value as SkillListSort })}
          >
            {sortOptions.map((sort) => <option key={sort} value={sort}>{t(`skills.sort.${sort}`)}</option>)}
          </NativeSelect>
        </label>
      </div>
      {children && <div className="skill-library-filter__results">{children}</div>}
    </Surface>
  )
}
