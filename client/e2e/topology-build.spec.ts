import { test, expect } from "@playwright/test";

const ADMIN_EMAIL = "admin@test.com";
const ADMIN_PASSWORD = "Admin123!";

test.describe("topology build", () => {
    test.describe.configure({ mode: "serial" });

    test("zones, loads, scoring, conflict", async ({ page }) => {
        test.setTimeout(120_000);

        // ── 1. Login as admin ────────────────────────────────────────────────
        await page.goto("http://localhost:5173/login");
        await page.fill('[data-testid="login-email"]', ADMIN_EMAIL);
        await page.fill('[data-testid="login-password"]', ADMIN_PASSWORD);
        await page.click('[data-testid="login-submit"]');
        await page.waitForURL("**/overview");

        // ── 2. Create 3-level zone hierarchy ──────────────────────────────────
        await page.goto("http://localhost:5173/topology");
        await page.waitForLoadState("networkidle");

        const treeText = await page.locator('[data-testid="zone-tree"]').textContent();

        if (treeText?.includes("No zones configured")) {
            console.log("Creating zones...");

            await page.fill('[data-testid="zone-name-input"]', "Main Building");
            await page.selectOption('[data-testid="zone-type-select"]', "building");
            await page.click('[data-testid="zone-create-button"]');
            await page.waitForTimeout(1000);
            await expect(page.locator('[data-testid="zone-tree"]')).toContainText("Main Building", { timeout: 10000 });

            await page.fill('[data-testid="zone-name-input"]', "Floor 1");
            await page.selectOption('[data-testid="zone-type-select"]', "floor");
            await page.selectOption('[data-testid="zone-parent-select"]', { label: "Main Building" });
            await page.click('[data-testid="zone-create-button"]');
            await page.waitForTimeout(1000);
            await expect(page.locator('[data-testid="zone-tree"]')).toContainText("Floor 1", { timeout: 10000 });

            await page.fill('[data-testid="zone-name-input"]', "Server Room");
            await page.selectOption('[data-testid="zone-type-select"]', "room");
            await page.selectOption('[data-testid="zone-parent-select"]', { label: "Floor 1" });
            await page.click('[data-testid="zone-create-button"]');
            await page.waitForTimeout(1000);
            await expect(page.locator('[data-testid="zone-tree"]')).toContainText("Server Room", { timeout: 10000 });
        } else {
            console.log("Zones already exist, skipping creation.");
        }

        // ── 3. Load 1 — Manual P1 ─────────────────────────────────────────────
        await page.waitForSelector('[data-testid="load-name"]', { timeout: 10000 });

        await page.fill('[data-testid="load-name"]', "Critical Load");
        await page.selectOption('[data-testid="load-zone"]', { label: "Server Room" });
        await page.fill('[data-testid="load-relay-address"]', "1");
        await page.fill('[data-testid="load-power-rating"]', "50");
        await page.check('[data-testid="mode-manual"]');
        await page.selectOption('[data-testid="manual-priority-select"]', "P1");

        // ✅ Wait for network response instead of URL
        const responsePromise = page.waitForResponse(
            response => response.url().includes('/api/v1/loads') && response.status() === 201,
            { timeout: 15000 }
        );

        await page.click('[data-testid="save-button"]');
        await responsePromise;

        // ── 4. Load 2 — Auto-assign Q1=9, Q2=7, Q3=6 → P2 ─────────────────────
        await page.click('[data-testid="new-load-button"]');
        await page.waitForSelector('[data-testid="load-name"]', { timeout: 5000 });

        await page.fill('[data-testid="load-name"]', "Scored Load");
        await page.selectOption('[data-testid="load-zone"]', { label: "Server Room" });
        await page.fill('[data-testid="load-relay-address"]', "2");
        await page.fill('[data-testid="load-power-rating"]', "30");
        await page.check('[data-testid="mode-auto"]');

        const responsePromise2 = page.waitForResponse(
            response => response.url().includes('/api/v1/loads') && response.status() === 201,
            { timeout: 15000 }
        );

        await page.click('[data-testid="save-button"]');
        await responsePromise2;

        // ── 5. Set sliders ──────────────────────────────────────────────────────
        await page.waitForSelector('[data-testid="slider-q1"]', { timeout: 5000 });

        await page.fill('[data-testid="slider-q1"]', "9");
        await page.fill('[data-testid="slider-q2"]', "7");
        await page.fill('[data-testid="slider-q3"]', "6");

        await page.waitForTimeout(1000);

        // Score = 78 → P2
        await expect(page.locator('[data-testid="priority-badge"]')).toHaveText("P2", {
            timeout: 15000,
        });

        const responsePromise3 = page.waitForResponse(
            response => response.url().includes('/api/v1/loads') && response.status() === 200,
            { timeout: 15000 }
        );

        await page.click('[data-testid="save-button"]');
        await responsePromise3;

        // ── 6. Load 3 — Relay conflict ─────────────────────────────────────────
        await page.click('[data-testid="new-load-button"]');

        await page.fill('[data-testid="load-name"]', "Conflict Load");
        await page.selectOption('[data-testid="load-zone"]', { label: "Server Room" });
        await page.fill('[data-testid="load-relay-address"]', "1");
        await page.fill('[data-testid="load-power-rating"]', "20");
        await page.check('[data-testid="mode-manual"]');
        await page.selectOption('[data-testid="manual-priority-select"]', "P3");

        await page.click('[data-testid="save-button"]');

        await expect(page.getByText(/assigned to 'Critical Load'/)).toBeVisible({ timeout: 10000 });
    });
});