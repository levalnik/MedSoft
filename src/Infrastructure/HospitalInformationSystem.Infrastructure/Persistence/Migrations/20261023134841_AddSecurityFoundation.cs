using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalInformationSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSecurityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "UserName" IS NULL
                        OR "UserName" !~ '^[A-Za-z0-9._@+-]{3,16}$'
                        OR "NormalizedUserName" IS NULL OR "NormalizedUserName" <> upper("UserName")) THEN
                        RAISE EXCEPTION 'Security upgrade: incompatible usernames. Run HospitalInformationSystem.Maintenance report and apply an explicit rename plan before migration.';
                    END IF;
                    IF (SELECT count(*) FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                        WHERE r."Name" = 'Administrator') > 1 THEN
                        RAISE EXCEPTION 'Security upgrade: multiple administrators. Select one administrator in the explicit maintenance plan.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "AspNetUsers" u JOIN "AspNetUserRoles" ur ON ur."UserId" = u."Id"
                        JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE r."Name" = 'Administrator' AND u."MedicalWorkerId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Security upgrade: administrator must not be linked to a medical worker. Resolve this binding before migration.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "ck_medical_workers_first_name",
                table: "medical_workers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_medical_workers_last_name",
                table: "medical_workers");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "medical_workers",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "other");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUserName",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAdministrator",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAtUtc",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");

            migrationBuilder.CreateTable(
                name: "administrator_setup_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    administrator_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    totp_secret_protected = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_administrator_setup_state", x => x.id);
                    table.CheckConstraint("ck_setup_completion", "NOT is_completed OR (administrator_user_id IS NOT NULL AND totp_secret_protected IS NOT NULL AND completed_at_utc IS NOT NULL)");
                    table.CheckConstraint("ck_setup_dates", "updated_at_utc >= created_at_utc AND (completed_at_utc IS NULL OR completed_at_utc >= created_at_utc)");
                    table.CheckConstraint("ck_setup_singleton", "id = 1");
                    table.ForeignKey(
                        name: "FK_administrator_setup_state_AspNetUsers_administrator_user_id",
                        column: x => x.administrator_user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    replaced_by_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_sessions", x => x.id);
                    table.CheckConstraint("ck_refresh_dates", "expires_at_utc > created_at_utc AND (revoked_at_utc IS NULL OR revoked_at_utc >= created_at_utc)");
                    table.CheckConstraint("ck_refresh_hash", "token_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_refresh_replacement", "replaced_by_session_id IS NULL OR (revoked_at_utc IS NOT NULL AND replaced_by_session_id <> id)");
                    table.ForeignKey(
                        name: "FK_refresh_sessions_AspNetUsers_user_id",
                        column: x => x.user_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_refresh_sessions_refresh_sessions_replaced_by_session_id",
                        column: x => x.replaced_by_session_id,
                        principalTable: "refresh_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "security_audit_events",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_time_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    actor_kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    request_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    component = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_audit_events", x => x.event_id);
                    table.CheckConstraint("ck_audit_actor", "actor_kind IN ('user', 'anonymous', 'system') AND ((actor_kind = 'user' AND actor_user_id IS NOT NULL) OR (actor_kind <> 'user' AND actor_user_id IS NULL))");
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_medical_workers_first_name",
                table: "medical_workers",
                sql: "char_length(btrim(first_name)) BETWEEN 1 AND 24");

            migrationBuilder.AddCheckConstraint(
                name: "ck_medical_workers_last_name",
                table: "medical_workers",
                sql: "char_length(btrim(last_name)) BETWEEN 1 AND 24");

            migrationBuilder.AddCheckConstraint(
                name: "ck_medical_workers_role",
                table: "medical_workers",
                sql: "role IN ('doctor', 'laboratory-technician', 'nurse', 'other')");

            migrationBuilder.Sql("""
                UPDATE "AspNetUsers" u SET "IsAdministrator" = TRUE
                FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                WHERE u."Id" = ur."UserId" AND r."Name" = 'Administrator';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SingleAdministrator",
                table: "AspNetUsers",
                column: "IsAdministrator",
                unique: true,
                filter: "\"IsAdministrator\" = TRUE");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_admin_worker",
                table: "AspNetUsers",
                sql: "NOT \"IsAdministrator\" OR \"MedicalWorkerId\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_dates",
                table: "AspNetUsers",
                sql: "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_normalized_username",
                table: "AspNetUsers",
                sql: "\"NormalizedUserName\" IS NOT NULL AND \"NormalizedUserName\" = upper(\"UserName\")");

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_username",
                table: "AspNetUsers",
                sql: "\"UserName\" IS NOT NULL AND \"UserName\" ~ '^[A-Za-z0-9._@+-]{3,16}$'");

            migrationBuilder.CreateIndex(
                name: "IX_administrator_setup_state_administrator_user_id",
                table: "administrator_setup_state",
                column: "administrator_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_family_id",
                table: "refresh_sessions",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_replaced_by_session_id",
                table: "refresh_sessions",
                column: "replaced_by_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_token_hash",
                table: "refresh_sessions",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_refresh_sessions_user_id_revoked_at_utc",
                table: "refresh_sessions",
                columns: new[] { "user_id", "revoked_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_event_time_utc_event_id",
                table: "security_audit_events",
                columns: new[] { "event_time_utc", "event_id" });

            migrationBuilder.CreateIndex(
                name: "IX_security_audit_events_event_type_event_time_utc_event_id",
                table: "security_audit_events",
                columns: new[] { "event_type", "event_time_utc", "event_id" });

            migrationBuilder.Sql("""
                CREATE FUNCTION reject_security_audit_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Saved audit events are immutable' USING ERRCODE = '55000';
                END $$;
                CREATE TRIGGER security_audit_immutable BEFORE UPDATE OR DELETE ON security_audit_events
                    FOR EACH ROW EXECUTE FUNCTION reject_security_audit_change();
                CREATE TRIGGER security_audit_no_truncate BEFORE TRUNCATE ON security_audit_events
                    FOR EACH STATEMENT EXECUTE FUNCTION reject_security_audit_change();

                CREATE FUNCTION sync_administrator_marker() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP IN ('DELETE', 'UPDATE') THEN
                        IF EXISTS (SELECT 1 FROM "AspNetRoles" WHERE "Id" = OLD."RoleId" AND "Name" = 'Administrator') THEN
                            UPDATE "AspNetUsers" u SET "IsAdministrator" = FALSE WHERE u."Id" = OLD."UserId"
                                AND NOT EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                                    WHERE ur."UserId" = u."Id" AND r."Name" = 'Administrator');
                        END IF;
                    END IF;
                    IF TG_OP IN ('INSERT', 'UPDATE') THEN
                        IF EXISTS (SELECT 1 FROM "AspNetRoles" WHERE "Id" = NEW."RoleId" AND "Name" = 'Administrator') THEN
                            UPDATE "AspNetUsers" SET "IsAdministrator" = TRUE WHERE "Id" = NEW."UserId";
                        END IF;
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE TRIGGER user_roles_administrator_marker AFTER INSERT OR UPDATE OR DELETE ON "AspNetUserRoles"
                    FOR EACH ROW EXECUTE FUNCTION sync_administrator_marker();

                CREATE FUNCTION guard_administrator_marker() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NOT NEW."IsAdministrator" AND EXISTS (SELECT 1 FROM "AspNetUserRoles" ur JOIN "AspNetRoles" r ON r."Id" = ur."RoleId"
                        WHERE ur."UserId" = NEW."Id" AND r."Name" = 'Administrator') THEN
                        RAISE EXCEPTION 'Administrator marker must match the assigned role' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER users_guard_administrator_marker BEFORE UPDATE OF "IsAdministrator" ON "AspNetUsers"
                    FOR EACH ROW EXECUTE FUNCTION guard_administrator_marker();

                CREATE FUNCTION guard_administrator_role_name() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        IF OLD."Name" = 'Administrator' THEN
                            RAISE EXCEPTION 'The administrator role cannot be deleted' USING ERRCODE = '23514';
                        END IF;
                        RETURN OLD;
                    END IF;
                    IF (OLD."Name" = 'Administrator' OR NEW."Name" = 'Administrator') AND OLD."Name" IS DISTINCT FROM NEW."Name" THEN
                        RAISE EXCEPTION 'The administrator role cannot be renamed' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER roles_guard_administrator_name BEFORE UPDATE OF "Name" ON "AspNetRoles"
                    FOR EACH ROW EXECUTE FUNCTION guard_administrator_role_name();
                CREATE TRIGGER roles_guard_administrator_delete BEFORE DELETE ON "AspNetRoles"
                    FOR EACH ROW EXECUTE FUNCTION guard_administrator_role_name();

                CREATE FUNCTION validate_administrator_marker() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE marker boolean;
                BEGIN
                    SELECT "IsAdministrator" INTO marker FROM "AspNetUsers" WHERE "Id" = NEW."Id";
                    IF FOUND AND marker IS DISTINCT FROM EXISTS (SELECT 1 FROM "AspNetUserRoles" ur
                        JOIN "AspNetRoles" r ON r."Id" = ur."RoleId" WHERE ur."UserId" = NEW."Id" AND r."Name" = 'Administrator') THEN
                        RAISE EXCEPTION 'Administrator marker must match the assigned role at commit' USING ERRCODE = '23514';
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER users_validate_administrator_marker AFTER INSERT OR UPDATE ON "AspNetUsers"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION validate_administrator_marker();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER users_validate_administrator_marker ON "AspNetUsers";
                DROP FUNCTION validate_administrator_marker();
                DROP TRIGGER roles_guard_administrator_delete ON "AspNetRoles";
                DROP TRIGGER roles_guard_administrator_name ON "AspNetRoles";
                DROP FUNCTION guard_administrator_role_name();
                DROP TRIGGER users_guard_administrator_marker ON "AspNetUsers";
                DROP FUNCTION guard_administrator_marker();
                DROP TRIGGER user_roles_administrator_marker ON "AspNetUserRoles";
                DROP FUNCTION sync_administrator_marker();
                DROP FUNCTION reject_security_audit_change() CASCADE;
                """);
            migrationBuilder.DropTable(
                name: "administrator_setup_state");

            migrationBuilder.DropTable(
                name: "refresh_sessions");

            migrationBuilder.DropTable(
                name: "security_audit_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_medical_workers_first_name",
                table: "medical_workers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_medical_workers_last_name",
                table: "medical_workers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_medical_workers_role",
                table: "medical_workers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SingleAdministrator",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_admin_worker",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_dates",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_normalized_username",
                table: "AspNetUsers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_users_username",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "role",
                table: "medical_workers");

            migrationBuilder.DropColumn(
                name: "CreatedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsAdministrator",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "AspNetUsers");

            migrationBuilder.AlterColumn<string>(
                name: "UserName",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AlterColumn<string>(
                name: "NormalizedUserName",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(16)",
                oldMaxLength: 16);

            migrationBuilder.AddCheckConstraint(
                name: "ck_medical_workers_first_name",
                table: "medical_workers",
                sql: "char_length(btrim(first_name)) BETWEEN 3 AND 24");

            migrationBuilder.AddCheckConstraint(
                name: "ck_medical_workers_last_name",
                table: "medical_workers",
                sql: "char_length(btrim(last_name)) BETWEEN 3 AND 24");
        }
    }
}
