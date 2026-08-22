using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeLibrarySchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_domains_DomainId",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_sessions_SourceSessionId",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "FK_knowledge_workspaces_WorkspaceId",
                table: "knowledge");

            migrationBuilder.DropPrimaryKey(
                name: "PK_knowledge",
                table: "knowledge");

            migrationBuilder.RenameColumn(
                name: "Type",
                table: "knowledge",
                newName: "type");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "knowledge",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "knowledge",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Embedding",
                table: "knowledge",
                newName: "embedding");

            migrationBuilder.RenameColumn(
                name: "Content",
                table: "knowledge",
                newName: "content");

            migrationBuilder.RenameColumn(
                name: "Confidence",
                table: "knowledge",
                newName: "confidence");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "knowledge",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WorkspaceId",
                table: "knowledge",
                newName: "workspace_id");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "knowledge",
                newName: "updated_at_utc");

            migrationBuilder.RenameColumn(
                name: "SourceSessionId",
                table: "knowledge",
                newName: "source_session_id");

            migrationBuilder.RenameColumn(
                name: "LastUsedAtUtc",
                table: "knowledge",
                newName: "last_used_at_utc");

            migrationBuilder.RenameColumn(
                name: "IsPrivate",
                table: "knowledge",
                newName: "is_private");

            migrationBuilder.RenameColumn(
                name: "DomainId",
                table: "knowledge",
                newName: "domain_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "knowledge",
                newName: "created_at_utc");

            migrationBuilder.RenameColumn(
                name: "ConflictGroupId",
                table: "knowledge",
                newName: "conflict_group_id");

            migrationBuilder.RenameIndex(
                name: "IX_knowledge_WorkspaceId_SourceSessionId",
                table: "knowledge",
                newName: "ix_knowledge_workspace_id_source_session_id");

            migrationBuilder.RenameIndex(
                name: "IX_knowledge_SourceSessionId",
                table: "knowledge",
                newName: "ix_knowledge_source_session_id");

            migrationBuilder.RenameIndex(
                name: "IX_knowledge_Embedding",
                table: "knowledge",
                newName: "ix_knowledge_embedding_hnsw");

            migrationBuilder.RenameIndex(
                name: "IX_knowledge_DomainId_Status",
                table: "knowledge",
                newName: "ix_knowledge_domain_id_status");

            migrationBuilder.RenameIndex(
                name: "IX_knowledge_ConflictGroupId",
                table: "knowledge",
                newName: "ix_knowledge_conflict_group_id");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_confidence_decay_at_utc",
                table: "knowledge",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_knowledge",
                table: "knowledge",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_status_confidence_updated_at_utc_id",
                table: "knowledge",
                columns: new[] { "status", "confidence", "updated_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_status_created_at_utc_id",
                table: "knowledge",
                columns: new[] { "status", "created_at_utc", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_status_updated_at_utc_id",
                table: "knowledge",
                columns: new[] { "status", "updated_at_utc", "id" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_domains_domain_id",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_sessions_source_session_id",
                table: "knowledge");

            migrationBuilder.DropForeignKey(
                name: "fk_knowledge_workspaces_workspace_id",
                table: "knowledge");

            migrationBuilder.DropPrimaryKey(
                name: "pk_knowledge",
                table: "knowledge");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_status_confidence_updated_at_utc_id",
                table: "knowledge");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_status_created_at_utc_id",
                table: "knowledge");

            migrationBuilder.DropIndex(
                name: "ix_knowledge_status_updated_at_utc_id",
                table: "knowledge");

            migrationBuilder.DropColumn(
                name: "last_confidence_decay_at_utc",
                table: "knowledge");

            migrationBuilder.RenameColumn(
                name: "type",
                table: "knowledge",
                newName: "Type");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "knowledge",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "knowledge",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "embedding",
                table: "knowledge",
                newName: "Embedding");

            migrationBuilder.RenameColumn(
                name: "content",
                table: "knowledge",
                newName: "Content");

            migrationBuilder.RenameColumn(
                name: "confidence",
                table: "knowledge",
                newName: "Confidence");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "knowledge",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "workspace_id",
                table: "knowledge",
                newName: "WorkspaceId");

            migrationBuilder.RenameColumn(
                name: "updated_at_utc",
                table: "knowledge",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "source_session_id",
                table: "knowledge",
                newName: "SourceSessionId");

            migrationBuilder.RenameColumn(
                name: "last_used_at_utc",
                table: "knowledge",
                newName: "LastUsedAtUtc");

            migrationBuilder.RenameColumn(
                name: "is_private",
                table: "knowledge",
                newName: "IsPrivate");

            migrationBuilder.RenameColumn(
                name: "domain_id",
                table: "knowledge",
                newName: "DomainId");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "knowledge",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "conflict_group_id",
                table: "knowledge",
                newName: "ConflictGroupId");

            migrationBuilder.RenameIndex(
                name: "ix_knowledge_workspace_id_source_session_id",
                table: "knowledge",
                newName: "IX_knowledge_WorkspaceId_SourceSessionId");

            migrationBuilder.RenameIndex(
                name: "ix_knowledge_source_session_id",
                table: "knowledge",
                newName: "IX_knowledge_SourceSessionId");

            migrationBuilder.RenameIndex(
                name: "ix_knowledge_embedding_hnsw",
                table: "knowledge",
                newName: "IX_knowledge_Embedding");

            migrationBuilder.RenameIndex(
                name: "ix_knowledge_domain_id_status",
                table: "knowledge",
                newName: "IX_knowledge_DomainId_Status");

            migrationBuilder.RenameIndex(
                name: "ix_knowledge_conflict_group_id",
                table: "knowledge",
                newName: "IX_knowledge_ConflictGroupId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_knowledge",
                table: "knowledge",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_domains_DomainId",
                table: "knowledge",
                column: "DomainId",
                principalTable: "domains",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_sessions_SourceSessionId",
                table: "knowledge",
                column: "SourceSessionId",
                principalTable: "sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_knowledge_workspaces_WorkspaceId",
                table: "knowledge",
                column: "WorkspaceId",
                principalTable: "workspaces",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
