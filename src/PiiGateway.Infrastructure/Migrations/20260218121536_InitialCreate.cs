using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PiiGateway.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    plan = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    llm_provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    settings = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "varchar(50)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    refresh_token = table.Column<string>(type: "text", nullable: true),
                    refresh_token_expiry_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "varchar(50)", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    document_class = table.Column<string>(type: "varchar(50)", nullable: false),
                    sensitivity = table.Column<string>(type: "varchar(50)", nullable: false),
                    dpia_triggered = table.Column<bool>(type: "boolean", nullable: false),
                    dpia_acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    export_acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    second_scan_passed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    review_started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pseudonymized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    exported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.id);
                    table.ForeignKey(
                        name: "FK_jobs_organizations_organization_id",
                        column: x => x.organization_id,
                        principalTable: "organizations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_jobs_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action_type = table.Column<string>(type: "varchar(50)", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    entity_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    detection_source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    document_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    sensitivity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_log_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_log_users_actor_id",
                        column: x => x.actor_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "text_segments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    segment_index = table.Column<int>(type: "integer", nullable: false),
                    text_content = table.Column<string>(type: "text", nullable: false),
                    source_type = table.Column<string>(type: "varchar(50)", nullable: false),
                    source_location = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_text_segments", x => x.id);
                    table.ForeignKey(
                        name: "FK_text_segments_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pii_entities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    segment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_text_enc = table.Column<string>(type: "text", nullable: true),
                    replacement_text = table.Column<string>(type: "text", nullable: true),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_offset = table.Column<int>(type: "integer", nullable: false),
                    end_offset = table.Column<int>(type: "integer", nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    detection_sources = table.Column<string[]>(type: "text[]", nullable: false),
                    review_status = table.Column<string>(type: "varchar(50)", nullable: false),
                    reviewed_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pii_entities", x => x.id);
                    table.ForeignKey(
                        name: "FK_pii_entities_jobs_job_id",
                        column: x => x.job_id,
                        principalTable: "jobs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pii_entities_text_segments_segment_id",
                        column: x => x.segment_id,
                        principalTable: "text_segments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_pii_entities_users_reviewed_by_id",
                        column: x => x.reviewed_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_audit_job",
                table: "audit_log",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_audit_log_actor_id",
                table: "audit_log",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "idx_jobs_org_status",
                table: "jobs",
                columns: new[] { "organization_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_jobs_created_by_id",
                table: "jobs",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "idx_pii_job_status",
                table: "pii_entities",
                columns: new[] { "job_id", "review_status" });

            migrationBuilder.CreateIndex(
                name: "IX_pii_entities_reviewed_by_id",
                table: "pii_entities",
                column: "reviewed_by_id");

            migrationBuilder.CreateIndex(
                name: "IX_pii_entities_segment_id",
                table: "pii_entities",
                column: "segment_id");

            migrationBuilder.CreateIndex(
                name: "IX_text_segments_job_id",
                table: "text_segments",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_organization_id",
                table: "users",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "pii_entities");

            migrationBuilder.DropTable(
                name: "text_segments");

            migrationBuilder.DropTable(
                name: "jobs");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
