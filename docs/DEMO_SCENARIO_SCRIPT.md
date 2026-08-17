# BlackoutGuard V2 — Live Demo Scenario Script

**Scenario:** Injected frequency drop → automatic load shedding → automatic restoration → audited export
**Target duration:** ~5 minutes including the 30-second cooldown wait
**Presenter needs:** Working local environment (see prerequisites)

---

## 0. Prerequisites (do BEFORE the audience arrives)

| # | Check | Command / Where | Expected |
|---|---|---|---|
| 1 | PostgreSQL running | `pg_isready -h localhost` | `accepting connections` |
| 2 | API running | `dotnet run --project src/BlackoutGuard.Api` | `Now listening on http://localhost:5000` |
| 3 | Frontend running | `cd client && npm run dev` | Vite ready on `http://localhost:5173` |
| 4 | Seed data present | auto-seeded on API startup | Admin account exists |
| 5 | A P3 load + FREQ_LOW rule exist | Topology Config page | At least one active, sheddable load |

**Demo credentials:** `admin@test.com` / `Admin123!`

> **Pre-flight dry run:** walk steps 3–9 once before the real demo. Confirm the fault preset behaves as expected. If any step misbehaves, see Troubleshooting (§7).

---

## 1. Login as Admin (T+0:00)

| Step | Action | Click / Type |
|---|---|---|
| 1.1 | Open browser | `http://localhost:5173` |
| 1.2 | Redirect lands on Login page | — |
| 1.3 | Fill email | `admin@test.com` |
| 1.4 | Fill password | `Admin123!` |
| 1.5 | Click **Sign In** | — |

**Narration:** *"Everyone authenticates through the same gate. The JWT issued here carries the user's role and their facility_id — and from this moment, every API call and every SignalR channel is scoped to that facility."*

**Expected outcome:** Redirect to Live Overview (`/overview`). Sidebar visible with full Admin navigation: Live Overview, Topology Config, Rules Engine, **Simulator Panel**, Audit Logs, User Management.

> **Troubleshooting:** "Invalid email or password" → API's `DataSeeder` hasn't run; restart the API. Stuck on Login → dev console shows CORS error → check API is on port 5000.

---

## 2. Navigate to Simulator Panel (T+0:30)

| Step | Action | Click / Type |
|---|---|---|
| 2.1 | Click sidebar item | **Simulator Panel** |

**Narration:** *"The Simulator Panel is Admin-only. Operators and Viewers don't even see the menu item — and even if they guessed the URL, the route redirects them away. UI hiding is convenience; enforcement is server-side."*

**Expected outcome:** Page shows:
- Status pill **Live** (SignalR connected)
- Live Telemetry card: Frequency **50.00 Hz**, Voltage **230.0 V**, Load **— kW**, Generator **ON** (server-authoritative values)
- Controls: frequency slider (45–55 Hz), load slider (0–200%), generator toggle, red **Inject Fault** button

> **Troubleshooting:** Status shows "Connecting…" forever → API SignalR hub not mapped; confirm `/hubs/telemetry` is reachable and JWT is being attached.

---

## 3. Show Baseline (T+1:00)

| Step | Action | Expected |
|---|---|---|
| 3.1 | Point at Live Telemetry | Frequency 50.0 Hz, voltage ~230 V, generator ON |
| 3.2 | Point at Controls | Sliders at 50.0 Hz / 80% |
| 3.3 | (Optional) drag load slider to 80% | Live load value follows the *server echo*, not the slider |

**Narration:** *"This is steady state. Notice the telemetry numbers are not client-side estimates — every value on this card arrived over SignalR from the server. The client never echoes its own inputs."*

**Expected outcome:** All values stable, no alarms, nothing in the audit trail (check §5 later).

---

## 4. Inject Fault (T+1:30)

| Step | Action | Click / Type |
|---|---|---|
| 4.1 | Click | **Inject Fault (frequency_drop)** |

**Narration:** *"We now simulate a grid disturbance — a frequency drop event. In production this would come from real Modbus or MQTT telemetry; here we're driving the same pipeline with a simulated fault."*

**Expected outcome (within ~2 seconds):**
- Frequency on the Live Telemetry card **ramps down toward 47.5 Hz**
- A red **alarm** appears (`FREQ_CRITICAL`, severity Critical)
- The Decision Engine (100 ms tick loop) evaluates the FREQ_LOW rule

> **Troubleshooting:** Nothing happens → fault endpoint not implemented on the API yet; verify `/api/v1/simulator/fault` exists (see Task 5.3 follow-up) and check the API log for the POST.

---

## 5. Watch Automatic Shedding (T+1:35)

| Step | Action | Expected |
|---|---|---|
| 5.1 | Watch Live Telemetry | Frequency continues toward 47.5 Hz |
| 5.2 | Watch the relay indicator / load list | **P3 load de-energized** (shed) |
| 5.3 | Click sidebar | **Audit Logs** |

**Narration:** *"The engine sheds the lowest-priority load first — P3. Notice two things: first, hysteresis means we don't flap on noise; the condition must hold for a debounce window before acting. Second, every decision is written to the audit log with the full rationale."*

**Expected outcome (Audit Logs page):**
- New row at the **top**, highlighted, with:
  - **Event:** `Load Shedding Executed`
  - **Rationale:** e.g. *"Frequency below 48.0 Hz threshold"*
  - **Affected Load:** relay/name of the P3 load

> **Troubleshooting:** No new row → engine not registered as a hosted service, or the P3 load was already shed / cooldown-locked. Verify engine DI wiring.

---

## 6. Show the Audit Rationale (T+2:00)

| Step | Action | Expected |
|---|---|---|
| 6.1 | Point at the rationale column | Full human-readable text |
| 6.2 | Emphasize | Row arrived **live** over SignalR — no page refresh |

**Narration:** *"This row didn't require a refresh. The DecisionExecuted SignalR event pushed it into the table the moment the relay fired. And every entry is facility-scoped — two tenants watching this screen can never see each other's rows, not even over the live channel."*

**Expected outcome:** Rationale column shows the full decision text; Timestamp is a few seconds old.

---

## 7. Wait for Cooldown (T+2:30 → T+3:00)

| Step | Action | Expected |
|---|---|---|
| 7.1 | Narrate while waiting | ~30 seconds |
| 7.2 | (Optional) demo filler | Re-explain hysteresis: shed at 48.0, restore at 48.5 (gap = no flapping) |

**Narration:** *"While we wait, notice what the engine is NOT doing: it's not re-evaluating this load every tick into a frenzy. The load is in cooldown — locked, neither sheddable nor restorable — for the per-rule cooldown period. This is per-load, per-rule state, not a global constant."*

**Expected outcome:** Load stays shed throughout the cooldown window even though frequency is still low.

---

## 8. Automatic Restoration (T+3:00)

| Step | Action | Expected |
|---|---|---|
| 8.1 | Watch frequency recover | Fault scenario returns frequency toward 50 Hz |
| 8.2 | Watch the load | **Restored automatically** once frequency ≥ restore threshold (48.5 Hz) for the debounce window |
| 8.3 | Click **Audit Logs** | New **Load Restored** row at top with its own rationale |

**Narration:** *"Frequency recovered. The engine restored the load by itself — no human touched a button. And the restore is audited with the same rigor as the shed."*

**Expected outcome:** Second highlighted row: Event `Load Restored`, rationale *"Frequency recovered above restore threshold"*.

> **Troubleshooting:** Load not restoring → cooldown not yet expired, or restore threshold not reached; check the FREQ_LOW rule's min/max on Rules Engine.

---

## 9. Export the Audit Log (T+3:30)

| Step | Action | Click / Type |
|---|---|---|
| 9.1 | On Audit Logs page, locate | **Export CSV** / **Export PDF** buttons |
| 9.2 | Click | **Export PDF** |
| 9.3 | Open downloaded file | `audit.pdf` |

**Narration:** *"Everything you saw — the shed, the cooldown, the restore — is now in a PDF your compliance team can file. Export buttons are hidden for Viewers; only Admin and Operator roles see them. And the export respects the same facility scoping as the screen."*

**Expected outcome:** PDF contains a landscape table with columns Timestamp / Event / Rationale / Affected Load, showing the shed + restore rows (with the fault event if applicable).

**Optional add-on:** repeat with **Export CSV** → open in Excel to show the same rows.

---

## 10. Closing (T+4:00)

**Narration:** *"To recap: one injected fault triggered a scoped, rule-driven decision, executed through the only code path allowed to touch relays, audited with rationale, broadcast live over an isolated channel, and exported for compliance. Defense in depth at every layer."*

---

## 7. Troubleshooting Reference

| Symptom | Likely cause | Fix |
|---|---|---|
| Login fails | Seed data missing | Restart API; `DataSeeder` runs at startup |
| Simulator page redirects to /overview | Logged in as non-Admin | Use `admin@test.com` / `Admin123!` |
| "Connecting…" never becomes "Live" | SignalR hub unreachable / token missing | Confirm API port 5000; check dev console for 401 on `/hubs/telemetry` |
| Inject Fault does nothing | Simulator endpoints not yet implemented on API | Implement `POST /api/v1/simulator/fault` + `/telemetry` (follow-up to Task 5.3) |
| No shed occurs | No active P3 load, or rule missing, or engine not hosted | Create a P3 load + FREQ_LOW rule; verify `EngineBackgroundService` is registered in DI |
| Shed row missing in audit | Audit GET endpoint not implemented | Implement `GET /api/v1/audit` (follow-up to Task 5.4) |
| PDF export blank | No audit rows in range | Export without `from`/`to`, or widen range |
| Restore never happens | Cooldown longer than expected | Wait for the rule's `cooldown_seconds`; check rule values |

---

## Appendix — Key Files Involved

| Concern | Location |
|---|---|
| Hub + method names | `src/BlackoutGuard.Api/Hubs/TelemetryHub.cs` |
| Broadcast wiring | `src/BlackoutGuard.Api/Engine/SignalRTelemetryBroadcaster.cs` |
| Engine tick loop | `src/BlackoutGuard.Infrastructure/Engine/EngineBackgroundService.cs` |
| Hysteresis + cooldown | `src/BlackoutGuard.Domain/BusinessRules/HysteresisManager.cs` |
| Export endpoint | `src/BlackoutGuard.Api/Controllers/AuditExportController.cs` |
| Simulator UI | `client/src/pages/SimulatorPanel/SimulatorPanel.tsx` |
| Audit UI | `client/src/pages/AuditLogPage/AuditTable.tsx` |
| Export buttons | `client/src/pages/AuditLogPage/ExportButtons.tsx` |
