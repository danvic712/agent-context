import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { getTheme, saveTheme } from './lib/api'

export type ThemeMode = 'light' | 'dark' | 'system'

export type ThemeValue = 'light' | 'dark'

interface ThemeContextValue {
  /** The stored platform theme (light/dark/system) — what the user picked. */
  mode: ThemeMode
  /** The resolved theme actually applied to the document. */
  resolved: ThemeValue
  setMode: (mode: ThemeMode) => Promise<void>
  toggle: () => Promise<void>
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

const systemMedia = () => window.matchMedia('(prefers-color-scheme: dark)')

function resolve(mode: ThemeMode): ThemeValue {
  if (mode === 'system') {
    return systemMedia().matches ? 'dark' : 'light'
  }
  return mode
}

function apply(value: ThemeValue) {
  document.documentElement.dataset.theme = value
}

/**
 * Platform theme (T12): the DB setting settings.theme is the source of truth
 * (per-call, like the language); the localStorage copy is only a first-paint
 * cache. `system` follows the OS preference live via matchMedia.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [mode, setModeState] = useState<ThemeMode>(() => {
    const cached = localStorage.getItem('agent-context:theme')
    return cached === 'light' || cached === 'dark' || cached === 'system' ? cached : 'system'
  })
  const resolved = resolve(mode)

  // Load the platform theme from the DB (corrects the cached first paint).
  useEffect(() => {
    let cancelled = false
    getTheme()
      .then(({ theme }) => {
        if (cancelled) return
        const next = theme === 'light' || theme === 'dark' || theme === 'system' ? theme : 'system'
        setModeState(next)
        localStorage.setItem('agent-context:theme', next)
        apply(resolve(next))
      })
      .catch(() => {
        // DB unreachable — keep the cached value.
      })
    return () => {
      cancelled = true
    }
  }, [])

  // Keep `system` in sync with OS changes while it is selected.
  useEffect(() => {
    if (mode !== 'system') return
    const media = systemMedia()
    const onChange = () => apply(media.matches ? 'dark' : 'light')
    media.addEventListener('change', onChange)
    return () => media.removeEventListener('change', onChange)
  }, [mode])

  const setMode = async (next: ThemeMode) => {
    setModeState(next)
    localStorage.setItem('agent-context:theme', next)
    apply(resolve(next))
    try {
      await saveTheme(next)
    } catch {
      // Persist is best-effort at the toggle; the UI has already switched.
    }
  }

  const toggle = () => setMode(resolved === 'dark' ? 'light' : 'dark')

  return (
    <ThemeContext.Provider value={{ mode, resolved, setMode, toggle }}>
      {children}
    </ThemeContext.Provider>
  )
}

export function useTheme(): ThemeContextValue {
  const value = useContext(ThemeContext)
  if (!value) {
    throw new Error('useTheme must be used within <ThemeProvider>')
  }
  return value
}
