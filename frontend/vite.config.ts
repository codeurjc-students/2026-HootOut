import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
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
})
