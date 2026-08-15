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
    proxy: {
      // Dev: the REST API runs on the ASP.NET Core host (default 8080).
      '/api': 'http://localhost:8080',
    },
  },
  build: {
    // Production build lands directly in wwwroot so the ASP.NET host serves it.
    outDir: '../wwwroot',
    emptyOutDir: true,
  },
})
