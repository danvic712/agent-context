import { useEffect, useState } from 'react'
import { ArrowUpIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'

export function BackToTop() {
  const { t } = useTranslation()
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    const update = () => setVisible(window.scrollY > 360)
    update()
    window.addEventListener('scroll', update, { passive: true })
    return () => window.removeEventListener('scroll', update)
  }, [])

  const scrollToTop = () => {
    const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    window.scrollTo({ top: 0, behavior: reducedMotion ? 'auto' : 'smooth' })
  }

  return (
    <button
      type="button"
      className="ui-back-to-top"
      data-visible={visible || undefined}
      aria-hidden={!visible}
      tabIndex={visible ? 0 : -1}
      onClick={scrollToTop}
      aria-label={t('appShell.backToTop')}
    >
      <ArrowUpIcon className="size-4" aria-hidden="true" />
      <span className="sr-only">{t('appShell.backToTop')}</span>
    </button>
  )
}
