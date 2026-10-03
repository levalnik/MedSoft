using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalInformationSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeparateMedicalWorkerDbo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Меняется CLR-тип в snapshot EF; таблица и её данные остаются прежними.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Обратный переход также не требует изменения схемы PostgreSQL.
        }
    }
}
