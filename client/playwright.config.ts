import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  timeout: 55_000,
  expect: { timeout: 10_000 },
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [["list"]],
  use: {
    baseURL: "http://localhost:5173",
    trace: "retain-on-failure",
  },
  // webServer: [
  //   {
  //     command:
  //       "dotnet run --project ../src/BlackoutGuard.Api/BlackoutGuard.Api.csproj --launch-profile http",
  //     url: "http:localhost:5000/api/health",
  //     reuseExistingServer: true,
  //     timeout: 180_000,
  //   },
  //   {
  //     command: "npm run dev -- --port 5173 --strictPort",
  //     url: "http:localhost:5173",
  //     reuseExistingServer: true,
  //     timeout: 60_000,
  //   },
  // ],
  projects: [{ name: "chromium", use: { browserName: "chromium" } }],
});
