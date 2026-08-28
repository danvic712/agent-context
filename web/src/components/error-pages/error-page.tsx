import { AlertTriangleIcon, ArrowLeftIcon, HomeIcon, LeafIcon, RotateCcwIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { Button, buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import './error-page.css'

type RouteErrorKind = 'not-found' | 'unexpected'

interface ErrorPageProps {
  onRetry?: () => void
}

export function NotFoundPage() {
  return <RouteErrorPage kind="not-found" />
}

export function ErrorPage({ onRetry }: ErrorPageProps) {
  return <RouteErrorPage kind="unexpected" onRetry={onRetry} />
}

function RouteErrorPage({ kind, onRetry }: { kind: RouteErrorKind; onRetry?: () => void }) {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const navigate = useNavigate()
  const isNotFound = kind === 'not-found'
  const pageId = isNotFound ? 'not-found' : 'unexpected-error'
  const GlyphIcon = isNotFound ? LeafIcon : AlertTriangleIcon
  const handleRetry = () => {
    if (onRetry) {
      onRetry()
      return
    }
    window.location.reload()
  }

  return (
    <div className={cn('ui-page', 'error-page', isNotFound ? 'error-page--not-found' : 'error-page--unexpected')} data-ui-page>
      <section className="error-page__stage" aria-labelledby={`${pageId}-title`}>
        <div className="error-page__orbit" aria-hidden="true" />
        <div className="error-page__content">
          <div className="error-page__glyph" aria-hidden="true">
            <GlyphIcon />
          </div>
          <p className="error-page__eyebrow">{t(isNotFound ? 'errorPages.notFoundEyebrow' : 'errorPages.errorEyebrow')}</p>
          <div className="error-page__code" aria-hidden="true">{t(isNotFound ? 'errorPages.notFoundCode' : 'errorPages.errorCode')}</div>
          <h1 id={`${pageId}-title`} className="error-page__title">
            {t(isNotFound ? 'errorPages.notFoundTitle' : 'errorPages.errorTitle')}
          </h1>
          <p className="error-page__description">
            {t(isNotFound ? 'errorPages.notFoundDescription' : 'errorPages.errorDescription')}
          </p>
          <div className="error-page__actions">
            {isNotFound ? (
              <Link
                to="/knowledge"
                className={cn(buttonVariants({ variant: 'default', size: 'lg' }), 'error-page__button', 'error-page__button--primary')}
              >
                <HomeIcon aria-hidden="true" />
                {t('errorPages.returnToKnowledge')}
              </Link>
            ) : (
              <Button type="button" size="lg" className="error-page__button error-page__button--primary" onClick={handleRetry}>
                <RotateCcwIcon aria-hidden="true" />
                {t('errorPages.tryAgain')}
              </Button>
            )}
            {isNotFound ? (
              <Button type="button" variant="outline" size="lg" className="error-page__button error-page__button--secondary" onClick={() => navigate(-1)}>
                <ArrowLeftIcon aria-hidden="true" />
                {t('errorPages.goBack')}
              </Button>
            ) : (
              <Link
                to="/knowledge"
                className={cn(buttonVariants({ variant: 'outline', size: 'lg' }), 'error-page__button', 'error-page__button--secondary')}
              >
                <HomeIcon aria-hidden="true" />
                {t('errorPages.openKnowledge')}
              </Link>
            )}
          </div>
          <div className={cn('error-page__status', !isNotFound && 'error-page__status--error')}>
            <span className="error-page__status-dot" aria-hidden="true" />
            <span className="error-page__status-label">{t(isNotFound ? 'errorPages.safeRouteAvailable' : 'errorPages.retryAvailable')}</span>
            <span aria-hidden="true">·</span>
            <span>{t('errorPages.workspace')}</span>
            <span aria-hidden="true">/</span>
            <span className="error-page__requested-route">{t('errorPages.requestedRoute', { path: pathname || '/' })}</span>
          </div>
        </div>
      </section>
    </div>
  )
}
