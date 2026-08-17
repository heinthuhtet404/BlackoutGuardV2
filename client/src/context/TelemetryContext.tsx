import React, { createContext, useContext, useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { getAccessToken } from "../auth/tokenStore";

interface TelemetryContextType {
    connected: boolean;
    connection: signalR.HubConnection | null;
}

const TelemetryContext = createContext<TelemetryContextType>({
    connected: false,
    connection: null,
});

export const TelemetryProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const [connected, setConnected] = useState(false);
    const connectionRef = useRef<signalR.HubConnection | null>(null);

    useEffect(() => {
        let isMounted = true;

        // Connection ရှိပြီးသားဆိုလျှင် ထပ်မဖွင့်စေရန် စစ်ဆေးခြင်း
        if (connectionRef.current) return;

        const apiBase = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/api/v1";
        const hubBase = apiBase.replace(/\/api\/v1\/?$/, "");
        const hubUrl = `${hubBase}/hubs/telemetry`;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => getAccessToken() ?? "",
            })
            .withAutomaticReconnect()
            .build();

        connectionRef.current = connection;

        connection.onreconnecting(() => {
            if (isMounted) setConnected(false);
        });

        connection.onreconnected(() => {
            if (isMounted) setConnected(true);
        });

        connection.onclose(() => {
            if (isMounted) setConnected(false);
        });

        // Start connection safely
        connection
            .start()
            .then(() => {
                if (isMounted) {
                    setConnected(true);
                } else {
                    void connection.stop();
                }
            })
            .catch((err) => {
                if (isMounted && err.name !== "AbortError") {
                    console.error("SignalR Connection Error:", err);
                    setConnected(false);
                }
            });

        return () => {
            isMounted = false;

            // Connected သို့မဟုတ် Reconnecting ဖြစ်နေချိန်မှသာ safe stop ခေါ်မည်
            if (
                connection.state === signalR.HubConnectionState.Connected ||
                connection.state === signalR.HubConnectionState.Reconnecting
            ) {
                void connection.stop();
            }
            connectionRef.current = null;
        };
    }, []); // Empty dependency array - App Component Tree စတင်ချိန်မှသာ ၁ ခေါက်သာ run မည်

    return (
        <TelemetryContext.Provider value={{ connected, connection: connectionRef.current }}>
            {children}
        </TelemetryContext.Provider>
    );
};

export const useTelemetry = () => useContext(TelemetryContext);