import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Явно IPv4: по умолчанию Vite слушает только [::1], а он доступен не на всех машинах.
    host: '127.0.0.1',
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5141',
        changeOrigin: true,
      },
    },
  },
})
