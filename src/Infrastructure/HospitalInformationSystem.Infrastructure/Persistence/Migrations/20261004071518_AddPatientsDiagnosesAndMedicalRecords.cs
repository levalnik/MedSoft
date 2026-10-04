using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalInformationSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientsDiagnosesAndMedicalRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "diagnosis_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    name = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_diagnosis_definitions", x => x.id);
                    table.CheckConstraint("ck_diagnosis_definitions_code", "char_length(btrim(code)) = 6");
                    table.CheckConstraint("ck_diagnosis_definitions_name", "char_length(btrim(name)) BETWEEN 3 AND 24");
                });

            migrationBuilder.CreateTable(
                name: "patients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    middle_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sex = table.Column<int>(type: "integer", nullable: false),
                    comment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_patients", x => x.id);
                    table.CheckConstraint("ck_patients_comment", "comment IS NULL OR char_length(comment) <= 32");
                    table.CheckConstraint("ck_patients_first_name", "char_length(btrim(first_name)) BETWEEN 1 AND 64");
                    table.CheckConstraint("ck_patients_last_name", "char_length(btrim(last_name)) BETWEEN 1 AND 64");
                    table.CheckConstraint("ck_patients_middle_name", "middle_name IS NULL OR char_length(btrim(middle_name)) BETWEEN 1 AND 64");
                    table.CheckConstraint("ck_patients_sex", "sex IN (0, 1, 2)");
                });

            migrationBuilder.CreateTable(
                name: "medical_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_records", x => x.id);
                    table.CheckConstraint("ck_medical_records_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_medical_records_patients_patient_id",
                        column: x => x.patient_id,
                        principalTable: "patients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medical_record_diagnoses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medical_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    diagnosis_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    diagnosis_code_snapshot = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    diagnosis_name_snapshot = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    added_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    added_by_medical_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    removed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by_medical_worker_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_record_diagnoses", x => x.id);
                    table.CheckConstraint("ck_record_diagnoses_code", "char_length(diagnosis_code_snapshot) = 6");
                    table.CheckConstraint("ck_record_diagnoses_name", "char_length(btrim(diagnosis_name_snapshot)) BETWEEN 3 AND 24");
                    table.CheckConstraint("ck_record_diagnoses_removal", "(removed_at_utc IS NULL) = (removed_by_medical_worker_id IS NULL)");
                    table.ForeignKey(
                        name: "FK_medical_record_diagnoses_diagnosis_definitions_diagnosis_de~",
                        column: x => x.diagnosis_definition_id,
                        principalTable: "diagnosis_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_medical_record_diagnoses_medical_records_medical_record_id",
                        column: x => x.medical_record_id,
                        principalTable: "medical_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medical_record_diagnoses_medical_workers_added_by_medical_w~",
                        column: x => x.added_by_medical_worker_id,
                        principalTable: "medical_workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medical_record_diagnoses_medical_workers_removed_by_medical~",
                        column: x => x.removed_by_medical_worker_id,
                        principalTable: "medical_workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "medical_record_history_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medical_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    occurred_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    performed_by_medical_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    performed_by_name_snapshot = table.Column<string>(type: "character varying(49)", maxLength: 49, nullable: false),
                    performed_by_position_snapshot = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    event_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_record_history_events", x => x.id);
                    table.CheckConstraint("ck_record_history_type", "event_type IN ('MedicalRecordCreated', 'DiagnosisAdded', 'DiagnosisRemoved', 'PrescriptionAdded', 'PrescriptionCancelled')");
                    table.CheckConstraint("ck_record_history_version", "version > 0");
                    table.ForeignKey(
                        name: "FK_medical_record_history_events_medical_records_medical_recor~",
                        column: x => x.medical_record_id,
                        principalTable: "medical_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_medical_record_history_events_medical_workers_performed_by_~",
                        column: x => x.performed_by_medical_worker_id,
                        principalTable: "medical_workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prescriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    medical_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_medical_worker_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cancelled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_medical_worker_id = table.Column<Guid>(type: "uuid", nullable: true),
                    replaces_prescription_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prescriptions", x => x.id);
                    table.CheckConstraint("ck_prescriptions_cancellation", "(cancelled_at_utc IS NULL) = (cancelled_by_medical_worker_id IS NULL)");
                    table.CheckConstraint("ck_prescriptions_text", "char_length(btrim(text)) BETWEEN 3 AND 128");
                    table.ForeignKey(
                        name: "FK_prescriptions_medical_records_medical_record_id",
                        column: x => x.medical_record_id,
                        principalTable: "medical_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prescriptions_medical_workers_cancelled_by_medical_worker_id",
                        column: x => x.cancelled_by_medical_worker_id,
                        principalTable: "medical_workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prescriptions_medical_workers_created_by_medical_worker_id",
                        column: x => x.created_by_medical_worker_id,
                        principalTable: "medical_workers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_prescriptions_prescriptions_replaces_prescription_id",
                        column: x => x.replaces_prescription_id,
                        principalTable: "prescriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_diagnosis_definitions_code",
                table: "diagnosis_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_diagnosis_definitions_name",
                table: "diagnosis_definitions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_medical_record_diagnoses_added_by_medical_worker_id",
                table: "medical_record_diagnoses",
                column: "added_by_medical_worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_record_diagnoses_diagnosis_definition_id",
                table: "medical_record_diagnoses",
                column: "diagnosis_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_record_diagnoses_medical_record_id",
                table: "medical_record_diagnoses",
                column: "medical_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_record_diagnoses_removed_by_medical_worker_id",
                table: "medical_record_diagnoses",
                column: "removed_by_medical_worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_medical_record_history_events_performed_by_medical_worker_id",
                table: "medical_record_history_events",
                column: "performed_by_medical_worker_id");

            migrationBuilder.CreateIndex(
                name: "ux_record_history_version",
                table: "medical_record_history_events",
                columns: new[] { "medical_record_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_medical_records_patient_id",
                table: "medical_records",
                column: "patient_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prescriptions_cancelled_by_medical_worker_id",
                table: "prescriptions",
                column: "cancelled_by_medical_worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_prescriptions_created_by_medical_worker_id",
                table: "prescriptions",
                column: "created_by_medical_worker_id");

            migrationBuilder.CreateIndex(
                name: "IX_prescriptions_medical_record_id",
                table: "prescriptions",
                column: "medical_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_prescriptions_replaces_prescription_id",
                table: "prescriptions",
                column: "replaces_prescription_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medical_record_diagnoses");

            migrationBuilder.DropTable(
                name: "medical_record_history_events");

            migrationBuilder.DropTable(
                name: "prescriptions");

            migrationBuilder.DropTable(
                name: "diagnosis_definitions");

            migrationBuilder.DropTable(
                name: "medical_records");

            migrationBuilder.DropTable(
                name: "patients");
        }
    }
}
