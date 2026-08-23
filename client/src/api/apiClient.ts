import {
    getAccessToken,
    getRefreshToken,
    setTokens,
    clearTokens,
    initializeTokens,
    type TokenSet,
} from "../auth/tokenStore";

const API_BASE_URL: string =
    import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/api/v1";

export class ApiError extends Error {
    readonly status: number;

    constructor(status: number, message: string) {
        super(message);
        this.name = "ApiError";
        this.status = status;
    }
}

function buildHeaders(init?: RequestInit): Headers {
    const headers = new Headers(init?.headers);

    if (!headers.has("Content-Type")) {
        headers.set("Content-Type", "application/json");
    }

    const token = getAccessToken();
    if (token) {
        headers.set("Authorization", `Bearer ${token}`);
    }

    return headers;
}

async function refreshAccessToken(): Promise<boolean> {
    const refreshToken = getRefreshToken();
    if (!refreshToken) return false;

    try {
        const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ refreshToken }),
        });

        if (!response.ok) return false;

        const data = (await response.json()) as TokenSet;
        if (data?.accessToken) {
            setTokens(data);
            return true;
        }

        return false;
    } catch {
        return false;
    }
}

async function readErrorMessage(response: Response): Promise<string> {
    try {
        const data = await response.json();
        if (data?.error) return String(data.error);
        if (data?.message) return String(data.message);
        if (data?.title) return String(data.title);
    } catch {
        // body wasn't JSON; fall through
    }

    return `Request failed with status ${response.status}`;
}

export interface ApiInit extends RequestInit {
    skipAuthRefresh?: boolean;
}

async function request<T>(
    method: string,
    path: string,
    body?: unknown,
    init?: ApiInit
): Promise<T> {
    initializeTokens();

    const makeCall = (): Promise<Response> =>
        fetch(`${API_BASE_URL}${path}`, {
            ...init,
            method,
            headers: buildHeaders(init),
            body: body !== undefined ? JSON.stringify(body) : undefined,
        });

    let response: Response;
    try {
        response = await makeCall();
    } catch (err) {
        throw new ApiError(
            0,
            `Network error: unable to reach the API at ${API_BASE_URL}. ${err instanceof Error ? err.message : String(err)}`
        );
    }

    if (response.status === 401 && !init?.skipAuthRefresh) {
        const refreshed = await refreshAccessToken();
        if (!refreshed) {
            clearTokens();
            throw new ApiError(401, "Session expired. Please sign in again.");
        }

        try {
            response = await makeCall();
        } catch (err) {
            throw new ApiError(
                0,
                `Network error: unable to reach the API at ${API_BASE_URL}. ${err instanceof Error ? err.message : String(err)}`
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

export function post<T>(path: string, body: unknown, init?: ApiInit): Promise<T> {
    return request<T>("POST", path, body, init);
}

export function put<T>(path: string, body: unknown, init?: ApiInit): Promise<T> {
    return request<T>("PUT", path, body, init);
}

export function del<T>(path: string, init?: ApiInit): Promise<T> {
    return request<T>("DELETE", path, undefined, init);
}