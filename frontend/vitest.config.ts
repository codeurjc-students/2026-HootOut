import { fileURLToPath } from 'node:url'
import { mergeConfig, defineConfig, configDefaults } from 'vitest/config'
import viteConfig from './vite.config'

export default defineConfig((configEnv) =>
  mergeConfig(
    viteConfig(configEnv),
    defineConfig({
      test: {
        environment: 'jsdom',
        exclude: [...configDefaults.exclude, 'e2e/**'],
        root: fileURLToPath(new URL('./', import.meta.url)),
        coverage: {
          provider: 'v8',
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
    }),
  ),
) 