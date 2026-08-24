import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import {
    clearTokens,
    getAccessToken,
    getCurrentUser,
    getRefreshToken,
    initializeTokens,
    setTokens,
    subscribeTokenChange,
    type TokenSet,
} from "./tokenStore";
import { AuthContext, type RegisterPayload } from "./authTypes";
import { post } from "../api/apiClient";

function readAuthState() {
    initializeTokens();
    return {
        user: getCurrentUser(),
        accessToken: getAccessToken(),
        refreshToken: getRefreshToken(),
    };
}

export function AuthProvider({ children }: { children: ReactNode }) {
    const [auth, setAuth] = useState(readAuthState);
    const [isLoading, setIsLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const unsubscribe = subscribeTokenChange(() => {
            setAuth(readAuthState());
        });
        return unsubscribe;
    }, []);

    const login = useCallback(async (email: string, password: string) => {
        setIsLoading(true);
        setError(null);
        try {
            const tokens = await post<TokenSet>("/auth/login", { email, password }, {
                skipAuthRefresh: true,
            });
            setTokens(tokens);
        } catch (err) {
            const message = err instanceof Error ? err.message : "Login failed";
            setError(message);
            throw err;
        } finally {
            setIsLoading(false);
        }
    }, []);

    const register = useCallback(async (payload: RegisterPayload) => {
        setIsLoading(true);
        setError(null);
        try {
            // Backend မှ ပြန်လာသော accessToken, refreshToken နှင့် user object များကို setTokens ထဲ သိမ်းဆည်းသည်
            const tokens = await post<TokenSet>("/auth/register", payload, {
                skipAuthRefresh: true,
            });
            setTokens(tokens);
        } catch (err) {
            const message = err instanceof Error ? err.message : "Registration failed";
            setError(message);
            throw err;
        } finally {
            setIsLoading(false);
        }
    }, []);

    const logout = useCallback(() => {
        clearTokens();
        setError(null);
    }, []);

    const refresh = useCallback(async () => {
        const currentRefreshToken = getRefreshToken();
        if (!currentRefreshToken) return;

        setIsLoading(true);
        setError(null);
        try {
            const tokens = await post<TokenSet>(
                "/auth/refresh",
                { refreshToken: currentRefreshToken },
                { skipAuthRefresh: true }
            );
            setTokens(tokens);
        } catch (err) {
            // Refresh token သက်တမ်းကုန်သွားပါက tokens များကို ရှင်းထုတ်ပြီး Session ဖျက်ပေးသည်
            clearTokens();
            const message = err instanceof Error ? err.message : "Token refresh failed";
            setError(message);
            throw err;
        } finally {
            setIsLoading(false);
        }
    }, []);

    const value = useMemo(
        () => ({
            user: auth.user,
            accessToken: auth.accessToken,
            refreshToken: auth.refreshToken,
            isLoading,
            error,
            login,
            register,
            logout,
            refresh,
        }),
        [auth, isLoading, error, login, register, logout, refresh]
    );

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}