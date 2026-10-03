using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalInformationSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "medical_workers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    last_name = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    position = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_medical_workers", x => x.id);
                    table.CheckConstraint("ck_medical_workers_first_name", "char_length(btrim(first_name)) BETWEEN 3 AND 24");
                    table.CheckConstraint("ck_medical_workers_last_name", "char_length(btrim(last_name)) BETWEEN 3 AND 24");
                    table.CheckConstraint("ck_medical_workers_position", "char_length(btrim(position)) BETWEEN 3 AND 24");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "medical_workers");
        }
    }
}
