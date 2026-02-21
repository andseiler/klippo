using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiiGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGuestAndSeedPlayground : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_guest",
                table: "jobs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Seed Playground organization
            migrationBuilder.Sql(@"
                INSERT INTO organizations (id, name, created_at)
                VALUES ('a0000000-0000-0000-0000-000000000001', 'Playground', NOW())
                ON CONFLICT (id) DO NOTHING;
            ");

            // Seed Playground user (password hash is bcrypt of 'playground-nologin')
            migrationBuilder.Sql(@"
                INSERT INTO users (id, email, name, password_hash, organization_id, role, created_at)
                VALUES (
                    'a0000000-0000-0000-0000-000000000002',
                    'playground@demo.local',
                    'Playground User',
                    '$2a$12$LJ3m4ys3Gz8KG7Zkfh7VOeTMZGFCbaFnGDwWWxxdGm8GKXNnqJKVe',
                    'a0000000-0000-0000-0000-000000000001',
                    'Reviewer',
                    NOW()
                )
                ON CONFLICT (id) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_guest",
                table: "jobs");

            migrationBuilder.Sql("DELETE FROM users WHERE id = 'a0000000-0000-0000-0000-000000000002';");
            migrationBuilder.Sql("DELETE FROM organizations WHERE id = 'a0000000-0000-0000-0000-000000000001';");
        }
    }
}
