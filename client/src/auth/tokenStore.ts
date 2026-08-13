export type Role = "Admin" | "Operator" | "Viewer";

export interface AuthUser {
  id: string;
  email: string;
  role: Role;
  facilityId: string;
}

export interface TokenSet {
  accessToken: string;
  refreshToken: string;
  user: AuthUser;
}

const ACCESS_TOKEN_KEY = "blackoutguard.access_token";
const REFRESH_TOKEN_KEY = "blackoutguard.refresh_token";
const USER_KEY = "blackoutguard.user";

let accessToken: string | null = null;
let refreshToken: string | null = null;
let currentUser: AuthUser | null = null;
let initialized = false;

const listeners = new Set<() => void>();

function notify(): void {
  for (const listener of listeners) {
    listener();
  }
}

export function initializeTokens(): void {
  if (initialized) return;
  initialized = true;

  accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);
  refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);

  const userJson = localStorage.getItem(USER_KEY);
  if (userJson) {
    try {
      currentUser = JSON.parse(userJson) as AuthUser;
    } catch {
      currentUser = null;
    }
  }
}

export function getAccessToken(): string | null {
  return accessToken;
}

export function getRefreshToken(): string | null {
  return refreshToken;
}

export function getCurrentUser(): AuthUser | null {
  return currentUser;
}

export function setTokens(tokens: TokenSet): void {
  accessToken = tokens.accessToken;
  refreshToken = tokens.refreshToken;
  currentUser = tokens.user;

  localStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
  localStorage.setItem(USER_KEY, JSON.stringify(tokens.user));

  notify();
}

export function clearTokens(): void {
  accessToken = null;
  refreshToken = null;
  currentUser = null;

  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
  localStorage.removeItem(USER_KEY);

  notify();
}

export function subscribeTokenChange(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function resetTokenStoreForTests(): void {
  accessToken = null;
  refreshToken = null;
  currentUser = null;
  initialized = false;
}
