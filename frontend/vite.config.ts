import { fileURLToPath, URL } from 'node:url'
import fs from 'node:fs'
import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import tailwindcss from '@tailwindcss/vite'

function runtimeConfig(mode: string) {
  const file = path.resolve(__dirname, `config/config.${mode}.json`)
  if (!fs.existsSync(file)) {
    throw new Error(`Missing config file ${file} for mode "${mode}"`)
  }
  return {
    name: 'runtime-config',
    // npm run dev writes the config to ./config.json (dev)
    configureServer(server: any) {
      server.middlewares.use('/config.json', (_req: any, res: any) => {
        res.setHeader('Content-Type', 'application/json')
        res.end(fs.readFileSync(file))
      })
    },
    // npm run build: writes the config on ./dist/config.json
    generateBundle(this: any) {
      this.emitFile({
        type: 'asset',
        fileName: 'config.json',
        source: fs.readFileSync(file, 'utf-8'),
      })
    },
  }
}

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
      target: env.VITE_API_PROXY_TARGET ?? 'https://localhost:8443',
      changeOrigin: true,
      secure: false
    },
    '/ws': {
      target: env.VITE_API_PROXY_TARGET ?? 'https://localhost:8443',
      changeOrigin: true,
      ws: true,
      secure: false
    }
  }
  return {
    plugins: [
      vue(),
      vueDevTools(),
      runtimeConfig(mode),
      tailwindcss()
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    build: {
      sourcemap: mode === 'test',
      minify: mode !== 'test',
    },
    server: { https, proxy, port: 5173, strictPort: true },
    preview: { https, proxy, port: 5173, strictPort: true },
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
