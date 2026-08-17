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

    // 1. Token Change ကို စောင့်ကြည့်ခြင်း
    useEffect(() => {
        const unsubscribe = subscribeTokenChange(() => {
            initializeTokens();
            setToken(getAccessToken());
        });
        return unsubscribe;
    }, []);

    // 2. Safe Async Connection Lifecycle Management
    useEffect(() => {
        if (!token) {
            if (connectionRef.current) {
                void connectionRef.current.stop();
                connectionRef.current = null;
            }
            setConnected(false);
            return;
        }

        let isMounted = true;

        const apiBase =
            import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/api/v1";
        const hubBase = apiBase.replace(/\/api\/v1\/?$/, "");
        const hubUrl = `${hubBase}/hubs/telemetry`;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => getAccessToken() ?? "",
            })
            .withAutomaticReconnect()
            .build();

        connectionRef.current = connection;

        connection.on("TelemetryUpdated", (payload: TelemetryUpdate) => {
            if (isMounted) setTelemetry(payload);
        });

        connection.onclose(() => {
            if (isMounted) setConnected(false);
        });

        connection.onreconnecting(() => {
            if (isMounted) setConnected(false);
        });

        connection.onreconnected(() => {
            if (isMounted) setConnected(true);
        });

        // Start connection and handle premature unmount safe cleanup
        connection
            .start()
            .then(() => {
                if (isMounted) {
                    setConnected(true);
                } else {
                    // If unmounted before start completed, stop cleanly after connection establishes
                    void connection.stop();
                }
            })
            .catch((err) => {
                if (isMounted) {
                    console.error("SignalR connection error:", err);
                    setConnected(false);
                }
            });

        return () => {
            isMounted = false;
            // Negotiate လုပ်နေတုန်း stop() မခေါ်ဘဲ Connected ဖြစ်ပြီးမှသာ stop() ကို ခေါ်ပါမည်
            if (connection.state === signalR.HubConnectionState.Connected) {
                void connection.stop();
            }
            connectionRef.current = null;
        };
    }, [token]);

    return { telemetry, connected };
}