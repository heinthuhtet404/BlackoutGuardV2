import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
    plugins: [react()],
    test: {
        globals: true,
        environment: 'jsdom',
        setupFiles: './src/test/setup.ts',
        exclude: [
            '**/node_modules/**',
            '**/dist/**',
            '**/e2e/**',  // ✅ E2E tests ကို ဖယ်ထုတ်ပါ
            '**/playwright-report/**',
            '**/test-results/**',
        ],
    },
})