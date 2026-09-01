import {
    getAccessToken,
    getRefreshToken,
    setTokens,
    clearTokens,
    initializeTokens,
    type TokenSet,
} from "../auth/tokenStore";

const API_BASE_URL: string = (
    import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/api/v1"
).replace(/\/+$/, ""); // Remove any trailing slashes from the base URL

export class ApiError extends Error {
    readonly status: number;

    constructor(status: number, message: string) {
        super(message);
        this.name = "ApiError";
        this.status = status;
    }
}

export interface ApiInit extends RequestInit {
    skipAuthRefresh?: boolean;
}

/**
  Separates custom wrapper options (like `skipAuthRefresh`)
  from standard browser RequestInit options to avoid fetch runtime errors.
 */
function extractNativeInit(init?: ApiInit): RequestInit {
    if (!init) return {};
    const { skipAuthRefresh, headers, ...rest } = init;
    return rest;
}

/**
  Constructs a standard Headers object with optional Authorization & JSON headers.
  Only attach Content-Type: application/json if there is actual body payload or HTTP Method allows body.
 */
function buildHeaders(method: string, init?: ApiInit, hasBody?: boolean): Headers {
    const headers = new Headers(init?.headers);

    // FIX: Only set Content-Type if request actually sends a payload (POST/PUT/PATCH)
    // To prevent Kestrel BadHttpRequestException on empty GET requests.
    if (hasBody && !headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
    }

    const token = getAccessToken();
    if (token) {
        headers.set("Authorization", `Bearer ${token}`);
    }

    return headers;
}

// Global state to manage concurrent 401 token refresh calls (Single-Flight Queue)
let isRefreshing = false;
let refreshPromise: Promise<boolean> | null = null;

async function refreshAccessToken(): Promise<boolean> {
    const refreshToken = getRefreshToken();
    if (!refreshToken) return false;

    // If a refresh request is already pending, reuse the existing promise
    if (isRefreshing && refreshPromise) {
        return refreshPromise;
    }

    isRefreshing = true;

    refreshPromise = (async () => {
        try {
            const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ refreshToken }),
            });

            if (!response.ok) {
                clearTokens();
                return false;
            }

            const data = (await response.json()) as TokenSet;
            if (data?.accessToken) {
                setTokens(data);
                return true;
            }

            clearTokens();
            return false;
        } catch (err) {
            console.error("[Auth Refresh Error]:", err);
            clearTokens();
            return false;
        } finally {
            isRefreshing = false;
            refreshPromise = null;
        }
    })();

    return refreshPromise;
}

async function readErrorMessage(response: Response): Promise<string> {
    try {
        const data = await response.json();
        if (data?.error) return String(data.error);
        if (data?.message) return String(data.message);
        if (data?.title) return String(data.title);
    } catch {
        // Response body was not valid JSON
    }

    return `Request failed with status ${response.status}`;
}

async function request<T>(
    method: string,
    path: string,
    body?: unknown,
    init?: ApiInit
): Promise<T> {
    initializeTokens();

    // Normalize path string to prevent double slashes or missing slashes
    const cleanPath = path ? (path.startsWith("/") ? path : `/${path}`) : "";
    const fullUrl = `${API_BASE_URL}${cleanPath}`;

    const hasBody = body !== undefined;
    const nativeInit = extractNativeInit(init);
    const headers = buildHeaders(method, init, hasBody);

    const requestOptions: RequestInit = {
        ...nativeInit,
        method,
        headers,
    };

    if (hasBody) {
        try {
            requestOptions.body = JSON.stringify(body);
        } catch (err) {
            throw new ApiError(
                0,
                `Invalid request body: ${err instanceof Error ? err.message : String(err)}`
            );
        }
    }

    let response: Response;
    try {
        response = await fetch(fullUrl, requestOptions);
    } catch (err: unknown) {
        // Handle explicit request aborts smoothly
        if (err instanceof Error && err.name === "AbortError") {
            throw err;
        }
        console.error(`[API Network Error] ${method} ${fullUrl}:`, err);
        throw new ApiError(
            0,
            `Network error: Unable to reach API at ${fullUrl}. Check if server is running or CORS is configured properly.`
        );
    }

    // Handle 401 Unauthorized & Token Refreshing
    if (response.status === 401 && !init?.skipAuthRefresh) {
        const refreshed = await refreshAccessToken();

        if (!refreshed) {
            clearTokens();
            throw new ApiError(401, "Session expired. Please sign in again.");
        }

        // Prepare single-retry request with updated Authorization header
        try {
            const retryInit: ApiInit = { ...init, skipAuthRefresh: true };
            const retryNativeInit = extractNativeInit(retryInit);
            const retryHeaders = buildHeaders(method, retryInit, hasBody);

            const retryOptions: RequestInit = {
                ...retryNativeInit,
                method,
                headers: retryHeaders,
            };

            if (hasBody) {
                retryOptions.body = JSON.stringify(body);
            }

            response = await fetch(fullUrl, retryOptions);
        } catch (err) {
            console.error(`[API Retry Network Error] ${method} ${fullUrl}:`, err);
            throw new ApiError(
                0,
                `Network error on retry: Unable to reach API at ${fullUrl}.`
            );
        }
    }

    if (!response.ok) {
        const message = await readErrorMessage(response);
        throw new ApiError(response.status, message);
    }

    if (response.status === 204) {
        return undefined as T;
    }

    return (await response.json()) as T;
}

export function get<T>(path: string, init?: ApiInit): Promise<T> {
    return request<T>("GET", path, undefined, init);
}

export function post<T>(
    path: string,
    body?: unknown,
    init?: ApiInit
): Promise<T> {
    return request<T>("POST", path, body, init);
}

export function put<T>(
    path: string,
    body?: unknown,
    init?: ApiInit
): Promise<T> {
    return request<T>("PUT", path, body, init);
}

export function del<T>(path: string, init?: ApiInit): Promise<T> {
    return request<T>("DELETE", path, undefined, init);
}