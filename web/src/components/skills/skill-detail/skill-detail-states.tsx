import { AlertCircleIcon, ArrowLeftIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Card, CardContent } from '@/components/ui/card'
import { PageFrame } from '@/components/ui/page-frame'
import { Skeleton } from '@/components/ui/skeleton'
import { buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'

export function SkillDetailLoadingState() {
  const { t } = useTranslation()

  return (
    <PageFrame>
      <div className="skill-detail-loading" aria-busy="true" aria-label={t('skills.loadingDetail')}>
        <div className="skill-detail-loading__header">
          <Skeleton className="h-3 w-32" />
          <Skeleton className="h-14 w-[min(30rem,80%)]" />
          <Skeleton className="h-4 w-[min(38rem,90%)]" />
        </div>
        <Card className="overflow-hidden">
          <CardContent className="grid gap-0 p-0 lg:grid-cols-[minmax(0,1fr)_300px]">
            <div className="space-y-3 border-b border-border/70 p-5 lg:border-b-0 lg:border-r">
              <Skeleton className="h-8 w-48" />
              <Skeleton className="h-4 w-full" />
              <Skeleton className="h-[320px] w-full rounded-xl" />
            </div>
            <div className="space-y-3 p-5">
              <Skeleton className="h-4 w-28" />
              {Array.from({ length: 5 }, (_, index) => <Skeleton key={index} className="h-8 w-full" />)}
            </div>
          </CardContent>
        </Card>
      </div>
    </PageFrame>
  )
}

export function SkillDetailErrorState({ error }: { error: string }) {
  const { t } = useTranslation()

  return (
    <PageFrame>
      <Card className="skill-detail-state">
        <CardContent className="flex flex-col items-center px-6 py-16 text-center">
          <div className="skill-detail-state__icon" aria-hidden="true">
            <AlertCircleIcon className="size-5" />
          </div>
          <Alert variant="destructive" className="mt-5 max-w-xl text-left">
            <AlertCircleIcon className="size-4" />
            <AlertTitle>{t('skills.detailLoadFailed')}</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
          <Link className={cn(buttonVariants({ variant: 'outline', size: 'sm' }), 'mt-6 no-underline')} to="/skills">
            <ArrowLeftIcon data-icon="inline-start" className="size-3.5" />
            {t('skills.backToLibrary')}
          </Link>
        </CardContent>
      </Card>
    </PageFrame>
  )
}
