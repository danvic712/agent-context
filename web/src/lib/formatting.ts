import i18n from '@/i18n'

function currentLocale() {
  return i18n.resolvedLanguage ?? i18n.language ?? 'en-US'
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
