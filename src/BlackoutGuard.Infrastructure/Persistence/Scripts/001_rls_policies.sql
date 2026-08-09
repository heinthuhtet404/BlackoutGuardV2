ALTER TABLE zones ENABLE ROW LEVEL SECURITY;
ALTER TABLE zones FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS facility_isolation ON zones;
CREATE POLICY facility_isolation ON zones
    USING (
        "FacilityId" = current_setting('app.current_facility_id', true)::uuid
    );


ALTER TABLE loads ENABLE ROW LEVEL SECURITY;
ALTER TABLE loads FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS facility_isolation ON loads;
CREATE POLICY facility_isolation ON loads
    USING (
        "FacilityId" = current_setting('app.current_facility_id', true)::uuid
    );


ALTER TABLE rules ENABLE ROW LEVEL SECURITY;
ALTER TABLE rules FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS facility_isolation ON rules;
CREATE POLICY facility_isolation ON rules
    USING (
        "FacilityId" = current_setting('app.current_facility_id', true)::uuid
    );


ALTER TABLE time_schedules ENABLE ROW LEVEL SECURITY;
ALTER TABLE time_schedules FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS facility_isolation ON time_schedules;
CREATE POLICY facility_isolation ON time_schedules
    USING (
        "FacilityId" = current_setting('app.current_facility_id', true)::uuid
    );


ALTER TABLE decision_audit_log ENABLE ROW LEVEL SECURITY;
ALTER TABLE decision_audit_log FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS facility_isolation ON decision_audit_log;
CREATE POLICY facility_isolation ON decision_audit_log
    USING (
        "FacilityId" = current_setting('app.current_facility_id', true)::uuid
    );


ALTER TABLE alarm_records ENABLE ROW LEVEL SECURITY;
ALTER TABLE alarm_records FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS facility_isolation ON alarm_records;
CREATE POLICY facility_isolation ON alarm_records
    USING (
        "FacilityId" = current_setting('app.current_facility_id', true)::uuid
    );


ALTER TABLE load_cooldown_state ENABLE ROW LEVEL SECURITY;
ALTER TABLE load_cooldown_state FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS facility_isolation ON load_cooldown_state;
CREATE POLICY facility_isolation ON load_cooldown_state
    USING (
        EXISTS (
            SELECT 1
            FROM loads
            WHERE loads."Id" = load_cooldown_state."LoadId"
              AND loads."FacilityId" =
                  current_setting('app.current_facility_id', true)::uuid
        )
    );