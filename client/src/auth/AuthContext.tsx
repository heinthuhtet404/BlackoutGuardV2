import { useCallback, useEffect, useState, type ReactNode } from "react";
import { registerAuthBridge } from "../api/apiClient";
import { AuthContext } from "./authContext";

const ACCESS_TOKEN_KEY = "blackoutguard.access_token";
const REFRESH_TOKEN_KEY = "blackoutguard.refresh_token";

export function AuthProvider({ children }: { children: ReactNode }) {
  const [token, setToken] = useState<string | null>(() =>
    localStorage.getItem(ACCESS_TOKEN_KEY)
  );

  const logout = useCallback(() => {
    localStorage.removeItem(ACCESS_TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    setToken(null);
  }, []);

  useEffect(() => {
    registerAuthBridge({
      getAccessToken: () => localStorage.getItem(ACCESS_TOKEN_KEY),
      getRefreshToken: () => localStorage.getItem(REFRESH_TOKEN_KEY),
      setTokens: (accessToken: string, refreshToken: string | null) => {
        localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
        if (refreshToken) {
          localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
        }
        setToken(accessToken);
      },
      logout,
    });
  }, [logout]);

  const value = {
    token,
    isAuthenticated: token !== null,
    logout,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
