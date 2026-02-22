using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PiiGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveRolesAndOrganizations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop FK from users → organizations
            migrationBuilder.Sql(@"
                ALTER TABLE users DROP CONSTRAINT IF EXISTS ""FK_users_organizations_organization_id"";
            ");

            // Drop FK from jobs → organizations
            migrationBuilder.Sql(@"
                ALTER TABLE jobs DROP CONSTRAINT IF EXISTS ""FK_jobs_organizations_organization_id"";
            ");

            // Drop index on jobs(organization_id, status)
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_jobs_org_status;
            ");

            // Drop index on users(organization_id)
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_users_organization_id"";
            ");

            // Drop organization_id column from users
            migrationBuilder.DropColumn(name: "organization_id", table: "users");

            // Drop organization_id column from jobs
            migrationBuilder.DropColumn(name: "organization_id", table: "jobs");

            // Drop role column from users
            migrationBuilder.DropColumn(name: "role", table: "users");

            // Drop organizations table
            migrationBuilder.DropTable(name: "organizations");

            // Create new index on jobs(created_by_id, status)
            migrationBuilder.Sql(@"
                CREATE INDEX idx_jobs_user_status ON jobs (created_by_id, status);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new index
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS idx_jobs_user_status;
            ");

            // Recreate organizations table
            migrationBuilder.Sql(@"
                CREATE TABLE organizations (
                    id uuid NOT NULL,
                    name character varying(255) NOT NULL,
                    plan character varying(50),
                    llm_provider character varying(50),
                    settings jsonb,
                    created_at timestamp with time zone NOT NULL,
                    CONSTRAINT ""PK_organizations"" PRIMARY KEY (id)
                );
            ");

            // Re-add role column to users
            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "users",
                type: "varchar(50)",
                nullable: false,
                defaultValue: "User");

            // Re-add organization_id column to users
            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Re-add organization_id column to jobs
            migrationBuilder.AddColumn<Guid>(
                name: "organization_id",
                table: "jobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // Recreate indexes
            migrationBuilder.Sql(@"
                CREATE INDEX ""IX_users_organization_id"" ON users (organization_id);
                CREATE INDEX idx_jobs_org_status ON jobs (organization_id, status);
            ");

            // Recreate foreign keys
            migrationBuilder.Sql(@"
                ALTER TABLE users ADD CONSTRAINT ""FK_users_organizations_organization_id""
                    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE;
                ALTER TABLE jobs ADD CONSTRAINT ""FK_jobs_organizations_organization_id""
                    FOREIGN KEY (organization_id) REFERENCES organizations(id) ON DELETE CASCADE;
            ");
        }
    }
}
