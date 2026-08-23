import { useTelemetry } from "../context/TelemetryContext";
import type { TelemetryUpdate } from "../context/TelemetryContext";

export type { TelemetryUpdate };

export interface UseTelemetryHubResult {
    telemetry: TelemetryUpdate | null;
    connected: boolean;
}

export function useTelemetryHub(): UseTelemetryHubResult {
    const { telemetry, connected } = useTelemetry();
    return { telemetry, connected };
}