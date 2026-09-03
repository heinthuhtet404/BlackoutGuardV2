import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AppShell } from "./components/layout/AppShell";
import { LoginPage } from "./pages/LoginPage/LoginPage";
import { RegisterPage } from "./pages/RegisterPage/RegisterPage";
import { TopologyConfigPage } from "./pages/TopologyConfigPage/TopologyConfigPage";
import { SimulatorPanel } from "./pages/SimulatorPanel/SimulatorPanel";
import { AuditTable } from "./pages/AuditLogPage/AuditTable";
import { RulesEnginePage } from "./pages/RulesEnginePage";
import { UserManagementPage } from "./pages/UserManagementPage";
import { LiveOverviewPage } from "./pages/LiveOverviewPage";
import { DashboardAnalyticsPage } from "./pages/DashboardAnalyticsPage";
import { TelemetryProvider } from "./context/TelemetryContext";
import "./App.css";

function App() {
    return (
        <BrowserRouter>
            <TelemetryProvider>
                <Routes>
                    <Route path="/register" element={<RegisterPage />} />
                    <Route path="/login" element={<LoginPage />} />
                    <Route element={<AppShell />}>
                        <Route path="/overview" element={<LiveOverviewPage />} />
                        <Route path="/dashboard" element={<DashboardAnalyticsPage />} />
                        <Route path="/topology" element={<TopologyConfigPage />} />
                        <Route path="/rules" element={<RulesEnginePage />} />
                        <Route path="/simulator" element={<SimulatorPanel />} />
                        <Route path="/audit" element={<AuditTable />} />
                        <Route path="/users" element={<UserManagementPage />} />
                    </Route>
                    <Route path="*" element={<Navigate to="/register" replace />} />
                </Routes>
            </TelemetryProvider>
        </BrowserRouter>
    );
}

export default App;