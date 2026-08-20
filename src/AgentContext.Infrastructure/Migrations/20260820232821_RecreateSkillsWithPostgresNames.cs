using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecreateSkillsWithPostgresNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_skills_domains_DomainId",
                table: "skills");

            migrationBuilder.DropForeignKey(
                name: "FK_skills_workspaces_WorkspaceId",
                table: "skills");

            migrationBuilder.DropPrimaryKey(
                name: "PK_skills",
                table: "skills");

            migrationBuilder.RenameColumn(
                name: "Version",
                table: "skills",
                newName: "version");

            migrationBuilder.RenameColumn(
                name: "Slug",
                table: "skills",
                newName: "slug");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "skills",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Instructions",
                table: "skills",
                newName: "instructions");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "skills",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "skills",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "skills",
                newName: "workspace_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "skills",
                newName: "updated_at_utc");

            migrationBuilder.RenameColumn(
                name: "DomainId",
                table: "skills",
                newName: "domain_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "skills",
                newName: "created_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_skills_WorkspaceId_DomainId_Slug_Version",
                table: "skills",
                newName: "uq_skills_workspace_id_domain_id_slug_version");

            migrationBuilder.RenameIndex(
                name: "IX_skills_WorkspaceId_DomainId_Slug",
                table: "skills",
                newName: "ix_skills_workspace_id_domain_id_slug");

            migrationBuilder.RenameIndex(
                name: "IX_skills_DomainId",
                table: "skills",
                newName: "ix_skills_domain_id");

            migrationBuilder.AddColumn<Guid>(
                name: "previous_version_id",
                table: "skills",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_digest",
                table: "skills",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_revision",
                table: "skills",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "skills",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_url",
                table: "skills",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_skills",
                table: "skills",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_skills_previous_version_id",
                table: "skills",
                column: "previous_version_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_skills_source_type",
                table: "skills",
                sql: "source_type IS NULL OR source_type IN ('manual', 'zip', 'skills_sh', 'local_copy')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_skills_version_positive",
                table: "skills",
                sql: "version >= 1");

            migrationBuilder.AddForeignKey(
                name: "fk_skills_domains_domain_id",
                table: "skills",
                column: "domain_id",
                principalTable: "domains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_skills_previous_version_id",
                table: "skills",
                column: "previous_version_id",
                principalTable: "skills",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_skills_workspaces_workspace_id",
                table: "skills",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_skills_domains_domain_id",
                table: "skills");

            migrationBuilder.DropForeignKey(
                name: "fk_skills_previous_version_id",
                table: "skills");

            migrationBuilder.DropForeignKey(
                name: "fk_skills_workspaces_workspace_id",
                table: "skills");

            migrationBuilder.DropPrimaryKey(
                name: "pk_skills",
                table: "skills");

            migrationBuilder.DropIndex(
                name: "ix_skills_previous_version_id",
                table: "skills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_skills_source_type",
                table: "skills");

            migrationBuilder.DropCheckConstraint(
                name: "ck_skills_version_positive",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "previous_version_id",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "source_digest",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "source_revision",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "skills");

            migrationBuilder.DropColumn(
                name: "source_url",
                table: "skills");

            migrationBuilder.RenameColumn(
                name: "version",
                table: "skills",
                newName: "Version");

            migrationBuilder.RenameColumn(
                name: "slug",
                table: "skills",
                newName: "Slug");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "skills",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "instructions",
                table: "skills",
                newName: "Instructions");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "skills",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "skills",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "workspace_id",
                table: "skills",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "updated_at_utc",
                table: "skills",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "domain_id",
                table: "skills",
                newName: "DomainId");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "skills",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "uq_skills_workspace_id_domain_id_slug_version",
                table: "skills",
                newName: "IX_skills_WorkspaceId_DomainId_Slug_Version");

            migrationBuilder.RenameIndex(
                name: "ix_skills_workspace_id_domain_id_slug",
                table: "skills",
                newName: "IX_skills_WorkspaceId_DomainId_Slug");

            migrationBuilder.RenameIndex(
                name: "ix_skills_domain_id",
                table: "skills",
                newName: "IX_skills_DomainId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_skills",
                table: "skills",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_skills_domains_DomainId",
                table: "skills",
                column: "DomainId",
                principalTable: "domains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_skills_workspaces_WorkspaceId",
                table: "skills",
                column: "WorkspaceId",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
