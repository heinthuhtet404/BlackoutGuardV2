using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BlackoutGuard.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialV2Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Plan = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "trial"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "facilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GeneratorCapacityKW = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_facilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_facilities_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.CheckConstraint("CK_users_role", "\"Role\" IN ('Admin','Operator','Viewer')");
                    table.ForeignKey(
                        name: "FK_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ParameterKey = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    MinValue = table.Column<double>(type: "double precision", nullable: false),
                    MaxValue = table.Column<double>(type: "double precision", nullable: false),
                    CooldownSeconds = table.Column<int>(type: "integer", nullable: false, defaultValue: 30),
                    Unit = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rules", x => x.Id);
                    table.CheckConstraint("CK_rules_cooldown_seconds", "\"CooldownSeconds\" >= 0");
                    table.CheckConstraint("CK_rules_parameter_key", "\"ParameterKey\" IN ('FREQ_LOW','FREQ_HIGH','VOLT_LOW','VOLT_HIGH','LOAD_SHED_TIMER')");
                    table.CheckConstraint("CK_rules_value_order", "\"MinValue\" <= \"MaxValue\"");
                    table.ForeignKey(
                        name: "FK_rules_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ParentZoneId = table.Column<Guid>(type: "uuid", nullable: true),
                    MetaData = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.Id);
                    table.CheckConstraint("CK_zones_type", "\"Type\" IN ('building','floor','room')");
                    table.ForeignKey(
                        name: "FK_zones_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_zones_zones_ParentZoneId",
                        column: x => x.ParentZoneId,
                        principalTable: "zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "alarm_records",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlarmCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Severity = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    State = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alarm_records", x => x.Id);
                    table.CheckConstraint("CK_alarm_records_severity", "\"Severity\" IN ('Info','Warning','Critical')");
                    table.ForeignKey(
                        name: "FK_alarm_records_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_alarm_records_users_AcknowledgedBy",
                        column: x => x.AcknowledgedBy,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "loads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ZoneId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RelayAddress = table.Column<int>(type: "integer", nullable: false),
                    PowerRatingKw = table.Column<double>(type: "double precision", nullable: false),
                    Priority = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    PriorityMode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false, defaultValue: "auto"),
                    CriticalityQ1 = table.Column<short>(type: "smallint", nullable: true),
                    CriticalityQ2 = table.Column<short>(type: "smallint", nullable: true),
                    CriticalityQ3 = table.Column<short>(type: "smallint", nullable: true),
                    CriticalityQ4 = table.Column<short>(type: "smallint", nullable: true),
                    CriticalityScore = table.Column<double>(type: "double precision", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    IsSheddable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loads", x => x.Id);
                    table.CheckConstraint("CK_loads_criticality_q1", "\"CriticalityQ1\" BETWEEN 1 AND 10");
                    table.CheckConstraint("CK_loads_criticality_q2", "\"CriticalityQ2\" BETWEEN 1 AND 10");
                    table.CheckConstraint("CK_loads_criticality_q3", "\"CriticalityQ3\" BETWEEN 1 AND 10");
                    table.CheckConstraint("CK_loads_criticality_q4", "\"CriticalityQ4\" BETWEEN 1 AND 10");
                    table.CheckConstraint("CK_loads_power_rating_kw", "\"PowerRatingKw\" >= 0");
                    table.CheckConstraint("CK_loads_priority", "\"Priority\" IN ('P1','P2','P3')");
                    table.CheckConstraint("CK_loads_priority_mode", "\"PriorityMode\" IN ('auto','manual')");
                    table.ForeignKey(
                        name: "FK_loads_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_loads_zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decision_audit_log",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    EventType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Rationale = table.Column<string>(type: "text", nullable: false),
                    AffectedLoadId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggeringFrequency = table.Column<double>(type: "double precision", nullable: true),
                    TriggeringVoltage = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_decision_audit_log", x => x.Id);
                    table.ForeignKey(
                        name: "FK_decision_audit_log_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_decision_audit_log_loads_AffectedLoadId",
                        column: x => x.AffectedLoadId,
                        principalTable: "loads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "load_cooldown_state",
                columns: table => new
                {
                    LoadId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastShedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastRestoredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CooldownUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_load_cooldown_state", x => x.LoadId);
                    table.ForeignKey(
                        name: "FK_load_cooldown_state_loads_LoadId",
                        column: x => x.LoadId,
                        principalTable: "loads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "time_schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LoadId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetPriority = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    DaysOfWeek = table.Column<short[]>(type: "smallint[]", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_time_schedules", x => x.Id);
                    table.CheckConstraint("CK_time_schedules_target_priority", "\"TargetPriority\" IN ('P1','P2','P3')");
                    table.ForeignKey(
                        name: "FK_time_schedules_facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_time_schedules_loads_LoadId",
                        column: x => x.LoadId,
                        principalTable: "loads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_alarms_facility_state",
                table: "alarm_records",
                columns: new[] { "FacilityId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_alarm_records_AcknowledgedBy",
                table: "alarm_records",
                column: "AcknowledgedBy");

            migrationBuilder.CreateIndex(
                name: "idx_audit_facility_time",
                table: "decision_audit_log",
                columns: new[] { "FacilityId", "TimestampUtc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_decision_audit_log_AffectedLoadId",
                table: "decision_audit_log",
                column: "AffectedLoadId");

            migrationBuilder.CreateIndex(
                name: "idx_facilities_tenant",
                table: "facilities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "idx_loads_facility",
                table: "loads",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "idx_loads_priority",
                table: "loads",
                columns: new[] { "FacilityId", "Priority" },
                filter: "\"IsActive\" = true");

            migrationBuilder.CreateIndex(
                name: "idx_loads_zone",
                table: "loads",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "uq_relay_per_facility",
                table: "loads",
                columns: new[] { "FacilityId", "RelayAddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_rules_facility",
                table: "rules",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "idx_schedules_facility",
                table: "time_schedules",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "idx_schedules_load",
                table: "time_schedules",
                column: "LoadId");

            migrationBuilder.CreateIndex(
                name: "uq_email_per_tenant",
                table: "users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_zones_facility",
                table: "zones",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "idx_zones_parent",
                table: "zones",
                column: "ParentZoneId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alarm_records");

            migrationBuilder.DropTable(
                name: "decision_audit_log");

            migrationBuilder.DropTable(
                name: "load_cooldown_state");

            migrationBuilder.DropTable(
                name: "rules");

            migrationBuilder.DropTable(
                name: "time_schedules");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "loads");

            migrationBuilder.DropTable(
                name: "zones");

            migrationBuilder.DropTable(
                name: "facilities");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
