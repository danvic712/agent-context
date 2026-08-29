import type { ReactNode } from 'react'
import { cn } from '@/lib/utils'
import { Skeleton } from './skeleton'
import './loading-skeletons.css'

interface LoadingRegionProps {
  label: string
  className?: string
  children: ReactNode
}

function LoadingRegion({ label, className, children }: LoadingRegionProps) {
  return (
    <div className={cn('ui-loading-region', className)} role="status" aria-busy="true" aria-label={label}>
      {children}
    </div>
  )
}

export function AppLoadingSkeleton({ label }: { label: string }) {
  return (
    <LoadingRegion label={label} className="ui-loading-app">
      <div className="ui-loading-app__topbar">
        <div className="ui-loading-app__topbar-inner">
          <div className="ui-loading-app__brand">
            <Skeleton className="size-8 rounded-[13px_17px_13px_17px]" />
            <div className="space-y-2">
              <Skeleton className="h-3.5 w-28" />
              <Skeleton className="h-2 w-40" />
            </div>
          </div>
          <div className="ui-loading-app__nav">
            <Skeleton className="h-9 w-28 rounded-full" />
            <Skeleton className="h-9 w-20 rounded-full" />
            <Skeleton className="h-9 w-24 rounded-full" />
          </div>
          <div className="ui-loading-app__utilities">
            <Skeleton className="h-8 w-24 rounded-full" />
            <Skeleton className="h-8 w-16 rounded-lg" />
            <Skeleton className="h-8 w-16 rounded-lg" />
          </div>
        </div>
      </div>
      <div className="ui-loading-app__page ui-page">
        <div className="ui-loading-app__heading">
          <Skeleton className="h-2.5 w-28" />
          <Skeleton className="mt-4 h-12 w-64 max-w-full" />
          <Skeleton className="mt-3 h-3.5 w-[min(34rem,80vw)] max-w-full" />
        </div>
        <div className="ui-loading-app__body">
          <div className="ui-loading-app__sidebar">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="mt-5 h-10 w-full rounded-xl" />
            <Skeleton className="mt-4 h-7 w-40 rounded-full" />
            {[0, 1, 2].map((item) => <Skeleton key={item} className="mt-3 h-20 w-full rounded-xl" />)}
          </div>
          <div className="ui-loading-app__content">
            <Skeleton className="h-4 w-28" />
            <Skeleton className="mt-5 h-10 w-3/5 max-w-full" />
            <Skeleton className="mt-3 h-3.5 w-full" />
            <Skeleton className="mt-2 h-3.5 w-4/5" />
            <Skeleton className="mt-9 h-44 w-full rounded-2xl" />
          </div>
        </div>
      </div>
    </LoadingRegion>
  )
}

export function KnowledgeListSkeleton({ label, count = 3 }: { label: string; count?: number }) {
  return (
    <LoadingRegion label={label} className="ui-loading-knowledge-list">
      {Array.from({ length: count }, (_, index) => (
        <div key={index} className="ui-loading-knowledge-row">
          <Skeleton className="ui-loading-knowledge-row__avatar size-8 rounded-xl" />
          <div className="ui-loading-knowledge-row__copy">
            <Skeleton className="h-3.5 w-3/4" />
            <Skeleton className="h-2.5 w-1/2" />
            <Skeleton className="h-2.5 w-full" />
          </div>
          <div className="ui-loading-knowledge-row__end">
            <Skeleton className="h-2.5 w-11" />
            <Skeleton className="size-3.5 rounded" />
          </div>
        </div>
      ))}
    </LoadingRegion>
  )
}

export function KnowledgeDetailSkeleton({ label }: { label: string }) {
  return (
    <LoadingRegion label={label} className="ui-loading-knowledge-detail">
      <div className="ui-loading-knowledge-detail__header">
        <Skeleton className="h-2.5 w-36" />
        <Skeleton className="mt-3 h-10 w-4/5 max-w-full" />
        <Skeleton className="mt-3 h-3 w-3/5 max-w-full" />
        <Skeleton className="mt-3 h-5 w-24 rounded-full" />
      </div>
      <div className="ui-loading-knowledge-detail__grid">
        <div className="ui-loading-knowledge-detail__main">
          <div className="ui-loading-knowledge-detail__reader-head">
            <Skeleton className="h-2.5 w-40" />
            <Skeleton className="h-5 w-20 rounded-full" />
          </div>
          <div className="ui-loading-knowledge-detail__body">
            <Skeleton className="h-2.5 w-20" />
            <Skeleton className="mt-4 h-4 w-full" />
            <Skeleton className="mt-2 h-4 w-5/6" />
            <Skeleton className="mt-2 h-4 w-3/5" />
            <Skeleton className="mt-8 h-20 w-full rounded-xl" />
            <Skeleton className="mt-9 h-10 w-full rounded-xl" />
          </div>
        </div>
        <aside className="ui-loading-knowledge-detail__context">
          <div className="flex items-center justify-between gap-3">
            <Skeleton className="h-5 w-28" />
            <Skeleton className="h-2.5 w-16" />
          </div>
          <div className="mt-4 border-b border-border/70 pb-4">
            <div className="flex items-center justify-between gap-3">
              <Skeleton className="h-2.5 w-14" />
              <Skeleton className="h-5 w-10" />
            </div>
            <Skeleton className="mt-3 h-2 w-full rounded-full" />
            <Skeleton className="mt-3 h-2.5 w-4/5" />
          </div>
          <Skeleton className="mt-4 h-10 w-full" />
          <Skeleton className="mt-3 h-10 w-full" />
          <Skeleton className="mt-3 h-10 w-full" />
        </aside>
      </div>
    </LoadingRegion>
  )
}

export function EngineMetricsSkeleton({ label }: { label: string }) {
  return (
    <LoadingRegion label={label} className="c-engine-metrics ui-loading-engine-metrics">
      {Array.from({ length: 5 }, (_, index) => (
        <div key={index} className="c-engine-metric" aria-hidden="true">
          <Skeleton className="h-3 w-16" />
          <Skeleton className="mt-3 h-7 w-12" />
          <Skeleton className="mt-2 h-3 w-20" />
        </div>
      ))}
    </LoadingRegion>
  )
}

export function SettingsInferenceSkeleton({ label }: { label: string }) {
  return (
    <LoadingRegion label={label} className="ui-loading-settings">
      <div className="ui-loading-settings__heading">
        <Skeleton className="h-5 w-44" />
        <Skeleton className="h-3 w-72 max-w-full" />
      </div>
      <div className="ui-loading-settings__grid">
        <div className="ui-loading-settings__panel">
          <Skeleton className="h-4 w-32" />
          <Skeleton className="mt-3 h-3 w-full" />
          <Skeleton className="mt-2 h-3 w-4/5" />
          <Skeleton className="mt-6 h-10 w-full rounded-lg" />
          <Skeleton className="mt-5 h-24 w-full rounded-xl" />
        </div>
        <div className="ui-loading-settings__panel">
          <Skeleton className="h-4 w-36" />
          <Skeleton className="mt-3 h-3 w-5/6" />
          <Skeleton className="mt-6 h-10 w-full rounded-lg" />
          <Skeleton className="mt-5 h-24 w-full rounded-xl" />
        </div>
      </div>
    </LoadingRegion>
  )
}

export function SkillLibraryListSkeleton({ label, count = 8 }: { label: string; count?: number }) {
  return (
    <LoadingRegion label={label} className="ui-loading-skill-list">
      {Array.from({ length: count }, (_, index) => (
        <div key={index} className="ui-loading-skill-list__row">
          <Skeleton className="size-8 rounded-xl" />
          <div className="min-w-0 flex-1 space-y-2">
            <Skeleton className="h-3.5 w-3/4" />
            <Skeleton className="h-2.5 w-1/2" />
          </div>
          <Skeleton className="h-3 w-7" />
        </div>
      ))}
    </LoadingRegion>
  )
}

export function SkillLibraryDetailSkeleton({ label }: { label: string }) {
  return (
    <LoadingRegion label={label} className="ui-loading-skill-detail">
      <div className="ui-loading-skill-detail__heading">
        <Skeleton className="h-3 w-32" />
        <Skeleton className="h-9 w-2/3 max-w-full" />
        <Skeleton className="h-4 w-full" />
      </div>
      <div className="ui-loading-skill-detail__reader">
        <div className="flex items-center gap-3 border-b border-border/70 p-4">
          <Skeleton className="size-8 rounded-xl" />
          <div className="min-w-0 flex-1 space-y-2">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="h-2.5 w-28" />
          </div>
        </div>
        <div className="space-y-4 p-6 sm:p-10">
          <Skeleton className="h-7 w-1/2" />
          <Skeleton className="h-4 w-full" />
          <Skeleton className="h-4 w-5/6" />
          <Skeleton className="h-4 w-4/6" />
          <Skeleton className="mt-8 h-32 w-full rounded-xl" />
        </div>
      </div>
    </LoadingRegion>
  )
}

export function SkillFilePreviewSkeleton({ label, compact = false }: { label: string; compact?: boolean }) {
  return (
    <LoadingRegion label={label} className={cn('ui-loading-file-preview', compact && 'ui-loading-file-preview--compact')}>
      <Skeleton className="h-5 w-2/5 max-w-full" />
      <Skeleton className="mt-5 h-3 w-full" />
      <Skeleton className="mt-2 h-3 w-5/6" />
      <Skeleton className="mt-2 h-3 w-4/6" />
      <Skeleton className="mt-7 h-28 w-full rounded-xl" />
    </LoadingRegion>
  )
}
