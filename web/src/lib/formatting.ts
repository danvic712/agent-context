import locale from '@/locale'

function currentLocale() {
  return locale.resolvedLanguage ?? locale.language ?? 'en-US'
}

export function formatDate(value: string) {
  return new Intl.DateTimeFormat(currentLocale(), {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(new Date(value))
}

export function formatDateTime(value: string) {
  return new Intl.DateTimeFormat(currentLocale(), {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value))
}
