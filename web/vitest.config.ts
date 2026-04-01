import { defineConfig } from 'vitest/config';
import path from 'node:path';

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    include: ['features/**/*.test.ts', 'features/**/*.test.tsx', 'app/**/*.test.ts', 'app/**/*.test.tsx', 'lib/**/*.test.ts', 'lib/**/*.test.tsx'],
    exclude: ['e2e/**']
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname)
    }
  }
});