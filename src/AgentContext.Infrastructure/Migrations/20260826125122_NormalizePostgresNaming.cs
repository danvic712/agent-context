using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePostgresNaming : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_domains_workspaces_WorkspaceId",
                table: "domains");

            migrationBuilder.DropForeignKey(
                name: "FK_memberships_users_UserId",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_memberships_workspaces_WorkspaceId",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "FK_sessions_domains_DomainId",
                table: "sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_sessions_workspaces_WorkspaceId",
                table: "sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_domains_domain_id",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_sessions_source_session_id",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_workspaces_workspace_id",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "fk_skills_domains_domain_id",
                table: "skills");

            migrationBuilder.DropForeignKey(
                name: "fk_skills_workspaces_workspace_id",
                table: "skills");

            migrationBuilder.DropForeignKey(
                name: "fk_usage_sessions_session_id",
                table: "usage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_workspaces",
                table: "workspaces");

            migrationBuilder.DropPrimaryKey(
                name: "PK_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_sessions",
                table: "sessions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_memberships",
                table: "memberships");

            migrationBuilder.DropPrimaryKey(
                name: "PK_domains",
                table: "domains");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "workspaces",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "workspaces",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "workspaces",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "workspaces",
                newName: "created_at_utc");

            migrationBuilder.RenameColumn(
                name: "Email",
                table: "users",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "users",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "PasswordHash",
                table: "users",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "users",
                newName: "display_name");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "users",
                newName: "created_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_users_Email",
                table: "users",
                newName: "uq_users_email");

            migrationBuilder.RenameColumn(
                name: "Task",
                table: "sessions",
                newName: "task");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "sessions",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Remembered",
                table: "sessions",
                newName: "remembered");

            migrationBuilder.RenameColumn(
                name: "Conclusion",
                table: "sessions",
                newName: "conclusion");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "sessions",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "sessions",
                newName: "workspace_id");

            migrationBuilder.RenameColumn(
                name: "SummaryJson",
                table: "sessions",
                newName: "summary_json");

            migrationBuilder.RenameColumn(
                name: "ProcessedAtUtc",
                table: "sessions",
                newName: "processed_at_utc");

            migrationBuilder.RenameColumn(
                name: "NextAttemptAtUtc",
                table: "sessions",
                newName: "next_attempt_at_utc");

            migrationBuilder.RenameColumn(
                name: "LastError",
                table: "sessions",
                newName: "last_error");

            migrationBuilder.RenameColumn(
                name: "FullContext",
                table: "sessions",
                newName: "full_context");

            migrationBuilder.RenameColumn(
                name: "ErrorCount",
                table: "sessions",
                newName: "error_count");

            migrationBuilder.RenameColumn(
                name: "DomainId",
                table: "sessions",
                newName: "domain_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "sessions",
                newName: "created_at_utc");

            migrationBuilder.RenameColumn(
                name: "AgentName",
                table: "sessions",
                newName: "agent_name");

            migrationBuilder.RenameIndex(
                name: "IX_sessions_WorkspaceId_DomainId",
                table: "sessions",
                newName: "ix_sessions_workspace_id_domain_id");

            migrationBuilder.RenameIndex(
                name: "IX_sessions_Status_NextAttemptAtUtc",
                table: "sessions",
                newName: "ix_sessions_status_next_attempt_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_sessions_DomainId",
                table: "sessions",
                newName: "ix_sessions_domain_id");

            migrationBuilder.RenameColumn(
                name: "Role",
                table: "memberships",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "memberships",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "memberships",
                newName: "workspace_id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "memberships",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "memberships",
                newName: "created_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_memberships_WorkspaceId_UserId",
                table: "memberships",
                newName: "uq_memberships_workspace_id_user_id");

            migrationBuilder.RenameIndex(
                name: "IX_memberships_UserId",
                table: "memberships",
                newName: "ix_memberships_user_id");

            migrationBuilder.RenameColumn(
                name: "Name",
                table: "domains",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "domains",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "domains",
                newName: "workspace_id");

            migrationBuilder.RenameColumn(
                name: "IsShared",
                table: "domains",
                newName: "is_shared");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "domains",
                newName: "created_at_utc");

            migrationBuilder.RenameIndex(
                name: "IX_domains_WorkspaceId_Name",
                table: "domains",
                newName: "uq_domains_workspace_id_name");

            migrationBuilder.AddPrimaryKey(
                name: "pk_workspaces",
                table: "workspaces",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_users",
                table: "users",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_sessions",
                table: "sessions",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_memberships",
                table: "memberships",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_domains",
                table: "domains",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_domains_workspaces_workspace_id",
                table: "domains",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_memberships_users_user_id",
                table: "memberships",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_memberships_workspaces_workspace_id",
                table: "memberships",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_sessions_domains_domain_id",
                table: "sessions",
                column: "domain_id",
                principalTable: "domains",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_sessions_workspaces_workspace_id",
                table: "sessions",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_domains_domain_id",
                table: "knowledge",
                column: "domain_id",
                principalTable: "domains",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_sessions_source_session_id",
                table: "knowledge",
                column: "source_session_id",
                principalTable: "sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_workspaces_workspace_id",
                table: "knowledge",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_skills_domains_domain_id",
                table: "skills",
                column: "domain_id",
                principalTable: "domains",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_skills_workspaces_workspace_id",
                table: "skills",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_usage_sessions_session_id",
                table: "usage",
                column: "session_id",
                principalTable: "sessions",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_domains_workspaces_workspace_id",
                table: "domains");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_users_user_id",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_memberships_workspaces_workspace_id",
                table: "memberships");

            migrationBuilder.DropForeignKey(
                name: "fk_sessions_domains_domain_id",
                table: "sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_sessions_workspaces_workspace_id",
                table: "sessions");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_domains_domain_id",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_sessions_source_session_id",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_workspaces_workspace_id",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "fk_skills_domains_domain_id",
                table: "skills");

            migrationBuilder.DropForeignKey(
                name: "fk_skills_workspaces_workspace_id",
                table: "skills");

            migrationBuilder.DropForeignKey(
                name: "fk_usage_sessions_session_id",
                table: "usage");

            migrationBuilder.DropPrimaryKey(
                name: "pk_workspaces",
                table: "workspaces");

            migrationBuilder.DropPrimaryKey(
                name: "pk_users",
                table: "users");

            migrationBuilder.DropPrimaryKey(
                name: "pk_sessions",
                table: "sessions");

            migrationBuilder.DropPrimaryKey(
                name: "pk_memberships",
                table: "memberships");

            migrationBuilder.DropPrimaryKey(
                name: "pk_domains",
                table: "domains");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "workspaces",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "workspaces",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "workspaces",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "workspaces",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "users",
                newName: "Email");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "users",
                newName: "PasswordHash");

            migrationBuilder.RenameColumn(
                name: "display_name",
                table: "users",
                newName: "DisplayName");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "users",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "uq_users_email",
                table: "users",
                newName: "IX_users_Email");

            migrationBuilder.RenameColumn(
                name: "task",
                table: "sessions",
                newName: "Task");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "sessions",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "remembered",
                table: "sessions",
                newName: "Remembered");

            migrationBuilder.RenameColumn(
                name: "conclusion",
                table: "sessions",
                newName: "Conclusion");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "sessions",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "workspace_id",
                table: "sessions",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "summary_json",
                table: "sessions",
                newName: "SummaryJson");

            migrationBuilder.RenameColumn(
                name: "processed_at_utc",
                table: "sessions",
                newName: "ProcessedAtUtc");

            migrationBuilder.RenameColumn(
                name: "next_attempt_at_utc",
                table: "sessions",
                newName: "NextAttemptAtUtc");

            migrationBuilder.RenameColumn(
                name: "last_error",
                table: "sessions",
                newName: "LastError");

            migrationBuilder.RenameColumn(
                name: "full_context",
                table: "sessions",
                newName: "FullContext");

            migrationBuilder.RenameColumn(
                name: "error_count",
                table: "sessions",
                newName: "ErrorCount");

            migrationBuilder.RenameColumn(
                name: "domain_id",
                table: "sessions",
                newName: "DomainId");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "sessions",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "agent_name",
                table: "sessions",
                newName: "AgentName");

            migrationBuilder.RenameIndex(
                name: "ix_sessions_workspace_id_domain_id",
                table: "sessions",
                newName: "IX_sessions_WorkspaceId_DomainId");

            migrationBuilder.RenameIndex(
                name: "ix_sessions_status_next_attempt_at_utc",
                table: "sessions",
                newName: "IX_sessions_Status_NextAttemptAtUtc");

            migrationBuilder.RenameIndex(
                name: "ix_sessions_domain_id",
                table: "sessions",
                newName: "IX_sessions_DomainId");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "memberships",
                newName: "Role");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "memberships",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "workspace_id",
                table: "memberships",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "memberships",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "memberships",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "uq_memberships_workspace_id_user_id",
                table: "memberships",
                newName: "IX_memberships_WorkspaceId_UserId");

            migrationBuilder.RenameIndex(
                name: "ix_memberships_user_id",
                table: "memberships",
                newName: "IX_memberships_UserId");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "domains",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "domains",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "workspace_id",
                table: "domains",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "is_shared",
                table: "domains",
                newName: "IsShared");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "domains",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameIndex(
                name: "uq_domains_workspace_id_name",
                table: "domains",
                newName: "IX_domains_WorkspaceId_Name");

            migrationBuilder.AddPrimaryKey(
                name: "PK_workspaces",
                table: "workspaces",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_users",
                table: "users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_sessions",
                table: "sessions",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_memberships",
                table: "memberships",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_domains",
                table: "domains",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_domains_workspaces_WorkspaceId",
                table: "domains",
                column: "WorkspaceId",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_memberships_users_UserId",
                table: "memberships",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_memberships_workspaces_WorkspaceId",
                table: "memberships",
                column: "WorkspaceId",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_sessions_domains_DomainId",
                table: "sessions",
                column: "DomainId",
                principalTable: "domains",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_sessions_workspaces_WorkspaceId",
                table: "sessions",
                column: "WorkspaceId",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_domains_domain_id",
                table: "knowledge",
                column: "domain_id",
                principalTable: "domains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_sessions_source_session_id",
                table: "knowledge",
                column: "source_session_id",
                principalTable: "sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_knowledge_workspaces_workspace_id",
                table: "knowledge",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_skills_domains_domain_id",
                table: "skills",
                column: "domain_id",
                principalTable: "domains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_skills_workspaces_workspace_id",
                table: "skills",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_usage_sessions_session_id",
                table: "usage",
                column: "session_id",
                principalTable: "sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
