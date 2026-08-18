import path from 'node:path'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    // The i18n store lives at the repo root (../../i18n, ADR 0008) — allow Vite
    // to serve files outside web/ so both frontend and backend read the same JSON.
    fs: { allow: ['..'] },
    proxy: {
      // Dev: the REST API runs on the ASP.NET Core host (default 8080).
      '/api': 'http://localhost:8080',
      // Keep the same-origin dashboard URL working when the React UI is served
      // by Vite instead of the ASP.NET host. WebSocket support is required by
      // the Aspire Blazor circuit.
      '/monitor': {
        target: 'http://localhost:8080',
        ws: true,
      },
      // Dashboard HTML injects this helper at the origin root.
      '/navfix.js': 'http://localhost:8080',
    },
  },
  build: {
    // Production build lands directly in the ASP.NET host's wwwroot so the
    // host serves the UI without any copy step.
    outDir: '../src/AgentContext.Host/wwwroot',
    emptyOutDir: true,
  },
})
