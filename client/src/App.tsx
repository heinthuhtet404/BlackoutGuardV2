import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AppShell } from "./components/layout/AppShell";
import { LoginPage } from "./pages/LoginPage/LoginPage";
import { TopologyConfigPage } from "./pages/TopologyConfigPage/TopologyConfigPage";
import "./App.css";

function PlaceholderPage({ title }: { title: string }) {
  return <h1>{title}</h1>;
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<AppShell />}>
          <Route path="/overview" element={<PlaceholderPage title="Live Overview" />} />
          <Route path="/topology" element={<TopologyConfigPage />} />
          <Route path="/rules" element={<PlaceholderPage title="Rules Engine" />} />
          <Route path="/simulator" element={<PlaceholderPage title="Simulator Panel" />} />
          <Route path="/audit" element={<PlaceholderPage title="Audit Logs" />} />
          <Route path="/users" element={<PlaceholderPage title="User Management" />} />
        </Route>
        <Route path="*" element={<Navigate to="/overview" replace />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;
