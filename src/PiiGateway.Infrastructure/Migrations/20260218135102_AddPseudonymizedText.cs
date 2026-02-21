using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiiGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPseudonymizedText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pseudonymized_text",
                table: "jobs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "pseudonymized_text",
                table: "jobs");
        }
    }
}
