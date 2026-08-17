import { useEffect, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { getAccessToken } from "../auth/tokenStore";

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
  const connectionRef = useRef<signalR.HubConnection | null>(null);

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

    connectionRef.current = connection;

    connection.on("TelemetryUpdated", (payload: TelemetryUpdate) => {
      setTelemetry(payload);
    });

    connection
      .start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    return () => {
      void connection.stop();
      connectionRef.current = null;
    };
  }, []);

  return { telemetry, connected };
}
