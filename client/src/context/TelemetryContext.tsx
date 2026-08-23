import React, { createContext, useContext, useEffect, useRef, useState } from "react";
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

export interface RelayDecision {
    relayAddress: number;
    energize: boolean;
}

export interface DecisionExecutedPayload {
    rationale: string;
    relayDecisions: RelayDecision[];
}

export interface TelemetryContextType {
    telemetry: TelemetryUpdate | null;
    connected: boolean;
    connection: signalR.HubConnection | null;
    latestDecision: DecisionExecutedPayload | null;
}

const TelemetryContext = createContext<TelemetryContextType>({
    telemetry: null,
    connected: false,
    connection: null,
    latestDecision: null,
});

export const TelemetryProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [telemetry, setTelemetry] = useState<TelemetryUpdate | null>(null);
    const [latestDecision, setLatestDecision] = useState<DecisionExecutedPayload | null>(null);
    const [connected, setConnected] = useState(false);
    const [token, setToken] = useState<string | null>(() => getAccessToken());
    const connectionRef = useRef<signalR.HubConnection | null>(null);

    // 1. App Startup Token Initialization & Subscription
    useEffect(() => {
        initializeTokens();
        setToken(getAccessToken());

        const unsubscribe = subscribeTokenChange(() => {
            const currentToken = getAccessToken();
            setToken((prev) => (prev !== currentToken ? currentToken : prev));
        });

        return () => {
            if (typeof unsubscribe === "function") {
                unsubscribe();
            }
        };
    }, []);

    // 2. Stable Singleton SignalR Connection Management
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
            .configureLogging(signalR.LogLevel.Warning)
            .build();

        connectionRef.current = connection;

        // Telemetry Update Event Listener
        connection.on("TelemetryUpdated", (payload: TelemetryUpdate) => {
            if (isMounted) {
                setTelemetry(payload);
            }
        });

        // Audit Log Decision Executed Event Listener
        connection.on("DecisionExecuted", (payload: DecisionExecutedPayload) => {
            if (isMounted) {
                setLatestDecision(payload);
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

            void startPromise.then(async () => {
                if (connection.state !== signalR.HubConnectionState.Disconnected) {
                    await connection.stop().catch(() => { });
                }
            });
        };
    }, [token]);

    return (
        <TelemetryContext.Provider
            value={{
                telemetry,
                connected,
                connection: connectionRef.current,
                latestDecision,
            }}
        >
            {children}
        </TelemetryContext.Provider>
    );
};

export const useTelemetry = () => useContext(TelemetryContext);