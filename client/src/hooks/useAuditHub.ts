import { useEffect, useRef } from "react";
import * as signalR from "@microsoft/signalr";
import { getAccessToken } from "../auth/tokenStore";

export interface DecisionExecutedPayload {
    relayDecisions: Array<{
        relayAddress: number;
        energize: boolean;
        rationale: string;
    }>;
    rationale: string;
}

interface UseAuditHubOptions {
    onDecisionExecuted: (payload: DecisionExecutedPayload) => void;
    onReconnected: () => void;
}

export function useAuditHub({ onDecisionExecuted, onReconnected }: UseAuditHubOptions) {
    const callbacksRef = useRef({ onDecisionExecuted, onReconnected });
    callbacksRef.current = { onDecisionExecuted, onReconnected };

    useEffect(() => {
        let isMounted = true;

        const apiBase = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5000/api/v1";
        const hubBase = apiBase.replace(/\/api\/v1\/?$/, "");
        const hubUrl = `${hubBase}/hubs/telemetry`;

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(hubUrl, {
                accessTokenFactory: () => getAccessToken() ?? "",
            })
            .withAutomaticReconnect()
            .build();

        connection.on("DecisionExecuted", (payload: DecisionExecutedPayload) => {
            if (isMounted) {
                callbacksRef.current.onDecisionExecuted(payload);
            }
        });

        connection.onreconnected(() => {
            if (isMounted) {
                callbacksRef.current.onReconnected();
            }
        });

        // Start connection with lifecycle safety
        connection
            .start()
            .then(() => {
                // Start ပြီးချိန်တွင် component unmount ဖြစ်သွားခဲ့ပါက တန်းပြီး stop လုပ်ပါမည်
                if (!isMounted) {
                    void connection.stop();
                }
            })
            .catch((err) => {
                // Unmount ကြောင့်ဖြစ်သော AbortError မဟုတ်ပါက မှတ်တမ်းတင်ပါမည်
                if (isMounted && err.name !== "AbortError") {
                    console.error("SignalR Audit Hub Error:", err);
                }
            });

        return () => {
            isMounted = false;

            // Connection state သည် Connected ဖြစ်နေမှသာ stop() ကို ခေါ်ပါမည် (Negotiation AbortError မတက်စေရန်)
            if (connection.state === signalR.HubConnectionState.Connected) {
                void connection.stop();
            }
        };
    }, []);
}