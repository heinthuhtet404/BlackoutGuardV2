import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import './index.css'
import App from './App.tsx'
import { AuthProvider } from './auth/AuthContext.tsx'
import { ToastProvider } from './components/ui/Toast.tsx'
import { TelemetryProvider } from './context/TelemetryContext.tsx' // 1. Import ပြုလုပ်ပါ

const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            retry: false,
            refetchOnWindowFocus: false,
        },
    },
})

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <QueryClientProvider client={queryClient}>
            <AuthProvider>
                <TelemetryProvider> {/* 2. AuthProvider ရဲ့ အတွင်းမှာ Wrap လုပ်ပါ */}
                    <ToastProvider>
                        <App />
                    </ToastProvider>
                </TelemetryProvider>
            </AuthProvider>
        </QueryClientProvider>
    </StrictMode>,
)