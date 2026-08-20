import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import {
    getAccessToken,
    initializeTokens,
    subscribeTokenChange,
} from "../auth/tokenStore";

export interface TelemetryUpdate {
    frequency: number;
    voltage: number;
    totalLoadKw: number;
    generatorOn: boolean;
}

export interface UseTelemetryHubResult {
    telemetry: TelemetryUpdate | null;
    connected: boolean;
}

export function useTelemetryHub(): UseTelemetryHubResult {
    const [telemetry, setTelemetry] = useState<TelemetryUpdate | null>(null);
    const [connected, setConnected] = useState(false);
    const [token, setToken] = useState<string | null>(() => {
        initializeTokens();
        return getAccessToken();
    });
    const connectionRef = useRef<signalR.HubConnection | null>(null);

    // 1. Token ပြောင်းလဲမှုကို စောင့်ကြည့်ခြင်း
    useEffect(() => {
        const unsubscribe = subscribeTokenChange(() => {
            setToken(getAccessToken());
        });
        return unsubscribe;
    }, []);

    // 2. Safe Async Connection Lifecycle Management
    useEffect(() => {
        if (!token) {
            if (connectionRef.current) {
                const conn = connectionRef.current;
                connectionRef.current = null;
                void conn.stop().catch(() => { });
            }
            setConnected(false);
            return;
        }

        let isMounted = true;
        let isCancelled = false;

        const apiBase =
            import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/api/v1";
        const hubBase = apiBase.replace(/\/api\/v1\/?$/, "").replace(/\/$/, "");
        const hubUrl = `${hubBase}/hubs/telemetry`;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => getAccessToken() ?? "",
            })
            .withAutomaticReconnect()
            .build();

        connectionRef.current = connection;

        connection.on("TelemetryUpdated", (payload: TelemetryUpdate) => {
            if (isMounted) {
                setTelemetry(payload);
            }
        });

        connection.onclose(() => {
            if (isMounted) {
                setConnected(false);
            }
        });

        connection.onreconnecting(() => {
            if (isMounted) {
                setConnected(false);
            }
        });

        connection.onreconnected(() => {
            if (isMounted) {
                setConnected(true);
            }
        });

        // Negotiating ပြုလုပ်နေစဉ် တခြားနေရာမှ ဖြတ်မရပ်စေရန် Safe Promise Wrapping
        const startPromise = (async () => {
            try {
                if (connection.state === signalR.HubConnectionState.Disconnected) {
                    await connection.start();
                }
                if (isCancelled) {
                    await connection.stop();
                } else if (isMounted) {
                    setConnected(true);
                }
            } catch (err) {
                if (!isCancelled && isMounted) {
                    console.error("SignalR connection error:", err);
                    setConnected(false);
                }
            }
        })();

        return () => {
            isMounted = false;
            isCancelled = true;

            if (connectionRef.current === connection) {
                connectionRef.current = null;
            }

            // connection.start() ပြီးဆုံးသည်အထိ စောင့်ပြီးမှသာ Safe ဖြစ်စွာ stop() ခေါ်မည်
            void startPromise.then(async () => {
                if (connection.state !== signalR.HubConnectionState.Disconnected) {
                    await connection.stop().catch(() => { });
                }
            });
        };
    }, [token]);

    return { telemetry, connected };
}