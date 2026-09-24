## Project Setup

```sh
npm install
```

### Compile and Hot-Reload for Development

```sh
npm run dev
```

### Type-Check, Compile and Minify for Production

```sh
npm run build
```

### Run Unit Tests with [Vitest](https://vitest.dev/)

```sh
npm run test:unit
```

### Run End-to-End Tests with [Playwright](https://playwright.dev)

```sh
# Install browsers for the first run
npx playwright install

# When testing on CI, must build the project first
npm run build

# Runs the end-to-end tests
npm run test:e2e
# Runs the tests only on Chromium
npm run test:e2e -- --project=chromium
# Runs the tests of a specific file
npm run test:e2e -- tests/example.spec.ts
# Runs the tests in debug mode
npm run test:e2e -- --debug
```

### Lint with [ESLint](https://eslint.org/)

```sh
npm run lint
```

### Install Sonar locally:

```sh
npm install -g @sonar/scan
```

### Run Sonar Scan locally:

```sh

npm run test:unit:coverage
sonar-scanner-npm -Dsonar.token=<sonar-token> -Dsonar.projectKey=<sonar-project-key> -Dsonar.organization=<sonar-organization-key> -Dsonar.javascript.lcov.reportPaths=coverage/vitest/lcov.info -Dsonar.coverage.exclusions=e2e/**,**/*.test.ts,**/*.spec.ts
```