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

export interface AuthBridge {
  getAccessToken(): string | null;
  getRefreshToken(): string | null;
  setTokens(accessToken: string, refreshToken: string | null): void;
  logout(): void;
}

let authBridge: AuthBridge | null = null;

export function registerAuthBridge(bridge: AuthBridge): void {
  authBridge = bridge;
}

function buildHeaders(init?: RequestInit): Headers {
  const headers = new Headers(init?.headers);

  if (!headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  const token = authBridge?.getAccessToken();
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  return headers;
}

async function refreshAccessToken(): Promise<boolean> {
  if (!authBridge) return false;

  const refreshToken = authBridge.getRefreshToken();
  if (!refreshToken) return false;

  try {
    const response = await fetch(`${API_BASE_URL}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });

    if (!response.ok) return false;

    const data = await response.json();
    if (data?.accessToken) {
      authBridge.setTokens(data.accessToken, data.refreshToken ?? null);
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

async function request<T>(
  method: string,
  path: string,
  body?: unknown,
  init?: RequestInit
): Promise<T> {
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

  if (response.status === 401) {
    const refreshed = await refreshAccessToken();
    if (!refreshed) {
      authBridge?.logout();
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

export function get<T>(path: string, init?: RequestInit): Promise<T> {
  return request<T>("GET", path, undefined, init);
}

export function post<T>(path: string, body: unknown, init?: RequestInit): Promise<T> {
  return request<T>("POST", path, body, init);
}

export function put<T>(path: string, body: unknown, init?: RequestInit): Promise<T> {
  return request<T>("PUT", path, body, init);
}

export function del<T>(path: string, init?: RequestInit): Promise<T> {
  return request<T>("DELETE", path, undefined, init);
}
