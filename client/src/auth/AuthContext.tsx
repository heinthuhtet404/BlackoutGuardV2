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
import { AuthContext } from "./authTypes";
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
      logout,
      refresh,
    }),
    [auth, isLoading, error, login, logout, refresh]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
