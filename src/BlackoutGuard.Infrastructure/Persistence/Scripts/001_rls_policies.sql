ALTER TABLE zones ENABLE ROW LEVEL SECURITY;
CREATE POLICY facility_isolation ON zones
    USING (facility_id = current_setting('app.current_facility_id')::uuid);

ALTER TABLE loads ENABLE ROW LEVEL SECURITY;
CREATE POLICY facility_isolation ON loads
    USING (facility_id = current_setting('app.current_facility_id')::uuid);

ALTER TABLE rules ENABLE ROW LEVEL SECURITY;
CREATE POLICY facility_isolation ON rules
    USING (facility_id = current_setting('app.current_facility_id')::uuid);

ALTER TABLE time_schedules ENABLE ROW LEVEL SECURITY;
CREATE POLICY facility_isolation ON time_schedules
    USING (facility_id = current_setting('app.current_facility_id')::uuid);

ALTER TABLE decision_audit_log ENABLE ROW LEVEL SECURITY;
CREATE POLICY facility_isolation ON decision_audit_log
    USING (facility_id = current_setting('app.current_facility_id')::uuid);

ALTER TABLE alarm_records ENABLE ROW LEVEL SECURITY;
CREATE POLICY facility_isolation ON alarm_records
    USING (facility_id = current_setting('app.current_facility_id')::uuid);

-- load_cooldown_state has no direct facility_id, join through loads
ALTER TABLE load_cooldown_state ENABLE ROW LEVEL SECURITY;
CREATE POLICY facility_isolation ON load_cooldown_state
    USING (EXISTS (
        SELECT 1 FROM loads
        WHERE loads.id = load_cooldown_state.load_id
        AND loads.facility_id = current_setting('app.current_facility_id')::uuid
    ));
