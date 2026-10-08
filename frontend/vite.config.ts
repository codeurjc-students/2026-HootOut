import { fileURLToPath, URL } from 'node:url'

import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import { readFileSync } from 'node:fs'

// https://vite.dev/config/
export default defineConfig(({ mode, command }) => {
  const env = loadEnv(mode, process.cwd(), '')

  const https =
    command === 'serve' && !process.env.VITEST
      ? {
        cert: readFileSync(env.TLS_CERT ?? '../certs/cert.pem'),
        key: readFileSync(env.TLS_KEY ?? '../certs/key.pem'),
      }
      : undefined

  const proxy = {
    '/api': {
      target: env.API_PROXY_TARGET ?? 'https://localhost:8443',
      changeOrigin: true,
      secure: false
    },
  }
  return {
    plugins: [
      vue(),
      vueDevTools(),
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: { https, proxy, port: 5173, strictPort: true },
    test: {
      coverage: {
        provider: 'v8', // or 'istanbul'
        reporter: ['lcov', 'text'],
        reportsDirectory: './coverage/vitest',
        exclude: [
          'coverage/**',
          'dist/**',
          '**/node_modules/**',

          // e2e folder
          'e2e/**',

          // test files themselves
          '**/*.test.ts',
          '**/*.test.tsx',
          '**/*.spec.ts',
          '**/*.spec.tsx',
          '**/__tests__/**',

          // common extras worth excluding too
          '**/*.d.ts',
          '**/*.config.ts',
          '**/mocks/**',
        ],
      },
    },
  }
})
