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
      callbacksRef.current.onDecisionExecuted(payload);
    });

    connection.onreconnected(() => {
      callbacksRef.current.onReconnected();
    });

    connection.start().catch(() => {
      // SignalR will keep retrying via withAutomaticReconnect.
    });

    return () => {
      void connection.stop();
    };
  }, []);
}
