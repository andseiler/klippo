using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiiGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobErrorMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "jobs",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "error_message",
                table: "jobs");
        }
    }
}
