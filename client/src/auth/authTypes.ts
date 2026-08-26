import { createContext, useContext } from "react";

export type Role = "Admin" | "Operator" | "Viewer";

// ========================================
// AUTH USER - Complete Type
// ========================================
export interface AuthUser {
    id: string;
    email: string;
    role: Role;
    fullName?: string;
    name?: string;
    username?: string;
    tenantId?: string;
    organizationName?: string;
    generatorCapacity?: number;
    facilityLocation?: string;
    createdAt?: string;
    updatedAt?: string;
}

// ========================================
// REGISTER PAYLOAD
// ========================================
export interface RegisterPayload {
    fullName: string;
    email: string;
    password: string;
    organizationName: string;
    generatorCapacity: number;
    facilityLocation?: string;
}

// ========================================
// LOGIN PAYLOAD
// ========================================
export interface LoginPayload {
    email: string;
    password: string;
}

// ========================================
// AUTH RESPONSE
// ========================================
export interface AuthResponse {
    user: AuthUser;
    accessToken: string;
    refreshToken: string;
}

// ========================================
// REFRESH TOKEN RESPONSE
// ========================================
export interface RefreshTokenResponse {
    accessToken: string;
    refreshToken?: string;
}

// ========================================
// AUTH CONTEXT VALUE
// ========================================
export interface AuthContextValue {
    user: AuthUser | null;
    accessToken: string | null;
    refreshToken: string | null;
    isLoading: boolean;
    error: string | null;
    login: (email: string, password: string) => Promise<void>;
    register: (payload: RegisterPayload) => Promise<void>;
    logout: () => void;
    refresh: () => Promise<void>;
    isAuthenticated: boolean;
    hasRole: (role: Role | Role[]) => boolean;
}

// ========================================
// AUTH CONTEXT
// ========================================
export const AuthContext = createContext<AuthContextValue | undefined>(undefined);

// ========================================
// USE AUTH HOOK
// ========================================
export function useAuth(): AuthContextValue {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error("useAuth must be used within an AuthProvider");
    }
    return context;
}

// ========================================
// USE ROLE HOOK (Simplified)
// ========================================
export function useRole(): { role: Role | null; is: (role: Role) => boolean } {
    const { user } = useAuth();
    const role = user?.role || null;
    return {
        role,
        is: (targetRole: Role) => role === targetRole,
    };
}

// ========================================
// HELPER: Get user display name
// ========================================
export function getUserDisplayName(user: AuthUser | null): string {
    if (!user) return "User";
    return user.fullName || user.name || user.username || user.email || "User";
}

// ========================================
// HELPER: Get user initial for avatar
// ========================================
export function getUserInitial(user: AuthUser | null): string {
    if (!user) return "U";
    const name = user.fullName || user.name || user.username || user.email || "User";
    return name.charAt(0).toUpperCase();
}

// ========================================
// HELPER: Check if user has role
// ========================================
export function hasRole(user: AuthUser | null, role: Role | Role[]): boolean {
    if (!user) return false;
    if (Array.isArray(role)) {
        return role.includes(user.role);
    }
    return user.role === role;
}

// ========================================
// HELPER: Check if user is admin
// ========================================
export function isAdmin(user: AuthUser | null): boolean {
    return user?.role === "Admin";
}

// ========================================
// HELPER: Check if user is operator
// ========================================
export function isOperator(user: AuthUser | null): boolean {
    return user?.role === "Operator";
}

// ========================================
// HELPER: Check if user is viewer
// ========================================
export function isViewer(user: AuthUser | null): boolean {
    return user?.role === "Viewer";
}

// ========================================
// TOKEN STORE (if needed separately)
// ========================================
export interface TokenStore {
    accessToken: string | null;
    refreshToken: string | null;
    setTokens: (accessToken: string, refreshToken: string) => void;
    clearTokens: () => void;
}