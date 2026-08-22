export type AppTab = 'knowledge' | 'skills' | 'analytics' | 'settings'

export const appTabs: readonly { id: AppTab; path: string }[] = [
  { id: 'knowledge', path: '/knowledge' },
  { id: 'skills', path: '/skills' },
  { id: 'analytics', path: '/analytics' },
  { id: 'settings', path: '/settings' },
]

const defaultTab: AppTab = 'knowledge'

export function getTabPath(tab: AppTab): string {
  return appTabs.find((item) => item.id === tab)?.path ?? '/knowledge'
}

export function getTabFromPath(pathname: string): AppTab {
  const normalizedPath = pathname.replace(/\/+$/, '') || '/'
  return appTabs.find((item) =>
    item.path === normalizedPath || normalizedPath.startsWith(`${item.path}/`),
  )?.id ?? defaultTab
}
