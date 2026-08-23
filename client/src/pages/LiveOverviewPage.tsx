import { useTelemetryHub } from "../hooks/useTelemetryHub";

export function LiveOverviewPage() {
    const { telemetry, connected } = useTelemetryHub();

    const isUnderFrequency = telemetry ? telemetry.frequency < 49.5 : false;

    return (
        <div style={{ padding: "1.5rem", maxWidth: "1000px", margin: "0 auto" }}>
            <h1>Live Overview</h1>

            <div style={{ marginBottom: "1rem" }}>
                <span style={{ color: connected ? "#10B981" : "#EF4444", fontWeight: "bold" }}>
                    {connected ? "● Live Stream Connected" : "○ Offline / Reconnecting..."}
                </span>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
                <div style={{ padding: "1.5rem", borderRadius: "8px", background: "#1E293B", color: "#FFF", border: isUnderFrequency ? "1px solid #EF4444" : "1px solid #334155" }}>
                    <h3 style={{ margin: 0, fontSize: "0.875rem", color: "#94A3B8" }}>Frequency</h3>
                    <p style={{ fontSize: "2rem", fontWeight: "bold", margin: "0.5rem 0", color: isUnderFrequency ? "#EF4444" : "#10B981" }}>
                        {telemetry ? `${telemetry.frequency.toFixed(2)} Hz` : "—"}
                    </p>
                    {isUnderFrequency && <span style={{ color: "#EF4444", fontSize: "0.75rem" }}>Warning: Under-Frequency Threshold</span>}
                </div>

                <div style={{ padding: "1.5rem", borderRadius: "8px", background: "#1E293B", color: "#FFF", border: "1px solid #334155" }}>
                    <h3 style={{ margin: 0, fontSize: "0.875rem", color: "#94A3B8" }}>Voltage</h3>
                    <p style={{ fontSize: "2rem", fontWeight: "bold", margin: "0.5rem 0", color: "#60A5FA" }}>
                        {telemetry ? `${telemetry.voltage.toFixed(1)} V` : "—"}
                    </p>
                </div>

                <div style={{ padding: "1.5rem", borderRadius: "8px", background: "#1E293B", color: "#FFF", border: "1px solid #334155" }}>
                    <h3 style={{ margin: 0, fontSize: "0.875rem", color: "#94A3B8" }}>Total Active Load</h3>
                    <p style={{ fontSize: "2rem", fontWeight: "bold", margin: "0.5rem 0", color: "#F59E0B" }}>
                        {telemetry ? `${telemetry.totalLoadKw.toFixed(1)} kW` : "—"}
                    </p>
                </div>

                <div style={{ padding: "1.5rem", borderRadius: "8px", background: "#1E293B", color: "#FFF", border: "1px solid #334155" }}>
                    <h3 style={{ margin: 0, fontSize: "0.875rem", color: "#94A3B8" }}>Generator Status</h3>
                    <p style={{ fontSize: "2rem", fontWeight: "bold", margin: "0.5rem 0", color: telemetry?.generatorOn ? "#10B981" : "#64748B" }}>
                        {telemetry ? (telemetry.generatorOn ? "ON" : "OFF") : "—"}
                    </p>
                </div>
            </div>
        </div>
    );
}