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
    gridOnline?: boolean;              // <-- ထည့်ရန်
    solarCapacityKw?: number;          // <-- ထည့်ရန်
    generatorCapacityKw?: number;
    engineTemp: number;
    fuelLevel: number;
    runtimeRemaining: number;
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

    // ⏱️ Engine Temp Update Time ကို 0.5s ~ 2s (500ms ~ 2000ms) ကြား ထိန်းချုပ်ရန် Ref များ
    const lastTempUpdateRef = useRef<number>(0);
    const nextDelayRef = useRef<number>(1000); // Initial Delay = 1.0s

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
            .configureLogging(signalR.LogLevel.Information)
            .build();

        connectionRef.current = connection;

        // Telemetry Payload Normalizer
        const handleTelemetryPayload = (payload: any) => {
            console.log("📡 SignalR Telemetry Received:", payload);
            if (isMounted && payload) {
                const isGenOn = payload.generatorOn ?? payload.GeneratorOn ?? false;
                const targetBaseTemp = isGenOn ? 82.5 : 30.0;
                const now = Date.now();

                setTelemetry((prev) => {
                    const prevTemp = prev?.engineTemp ?? targetBaseTemp;
                    let nextTemp = prevTemp;

                    // 🎯 Random Delay (500ms မှ 2000ms) ပြည့်မှသာ Temp ကို ပြောင်းလဲမည်
                    if (now - lastTempUpdateRef.current >= nextDelayRef.current) {
                        lastTempUpdateRef.current = now;

                        // ⏱️ နောက်တစ်ကြိမ် Update လုပ်မည့်ကြာချိန်ကို 0.5s မှ 2s (500ms မှ 2000ms) ကြား Random ပြန်သတ်မှတ်မည်
                        nextDelayRef.current = Math.floor(Math.random() * 1500) + 500;

                        // 🌡️ Temp ဂဏန်း အသစ်တွက်ချက်ခြင်း (0.5s မှ 2s အထိ ပြောင်းလဲနိုင်သောကြောင့် Fluctuations သဘာဝကျစေရန် Fluctuation နှုန်း ညှိထားပါသည်)
                        const microJitter = (Math.random() * 0.3 - 0.15);
                        const smoothedTemp = prevTemp + (targetBaseTemp - prevTemp) * 0.08 + microJitter;
                        nextTemp = Number(smoothedTemp.toFixed(1));
                    }

                    const defaultFuel = 78.0;
                    const defaultRuntime = (defaultFuel / 100) * 8.0;

                    return {
                        frequency: payload.frequency ?? payload.Frequency ?? 50.0,
                        voltage: payload.voltage ?? payload.Voltage ?? 230.0,
                        totalLoadKw: payload.totalLoadKw ?? payload.TotalLoadKw ?? 0,
                        generatorOn: isGenOn,
                        // SignalR မှ direct engineTemp ပါလာလျှင် ယူမည်၊ မပါပါက 0.5s-2s Interval Logic အတိုင်း ပြောင်းလဲပေးမည်
                        engineTemp: payload.engineTemp ?? payload.EngineTemp ?? nextTemp,
                        fuelLevel: payload.fuelLevel ?? payload.FuelLevel ?? defaultFuel,
                        runtimeRemaining: payload.runtimeRemaining ?? payload.RuntimeRemaining ?? defaultRuntime,
                    };
                });
            }
        };

        // Decision Payload Handler
        const handleDecisionPayload = (payload: DecisionExecutedPayload) => {
            console.log("⚡ SignalR Decision Executed:", payload);
            if (isMounted) {
                setLatestDecision(payload);
            }
        };

        connection.on("TelemetryUpdated", handleTelemetryPayload);
        connection.on("DecisionExecuted", handleDecisionPayload);

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
                    console.log("✅ SignalR Telemetry Hub Connected Successfully!");
                }
            } catch (err) {
                if (!isCancelled && isMounted) {
                    console.error("❌ SignalR connection error:", err);
                    setConnected(false);
                }
            }
        })();

        return () => {
            isMounted = false;
            isCancelled = true;

            connection.off("TelemetryUpdated", handleTelemetryPayload);
            connection.off("DecisionExecuted", handleDecisionPayload);

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