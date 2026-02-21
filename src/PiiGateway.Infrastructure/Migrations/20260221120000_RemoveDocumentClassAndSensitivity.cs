using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiiGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDocumentClassAndSensitivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_class",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "sensitivity",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "dpia_triggered",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "dpia_acknowledged",
                table: "jobs");

            migrationBuilder.DropColumn(
                name: "document_class",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "sensitivity",
                table: "audit_log");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_class",
                table: "jobs",
                type: "varchar(50)",
                nullable: false,
                defaultValue: "other");

            migrationBuilder.AddColumn<string>(
                name: "sensitivity",
                table: "jobs",
                type: "varchar(50)",
                nullable: false,
                defaultValue: "standard");

            migrationBuilder.AddColumn<bool>(
                name: "dpia_triggered",
                table: "jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "dpia_acknowledged",
                table: "jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "document_class",
                table: "audit_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sensitivity",
                table: "audit_log",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }
    }
}
