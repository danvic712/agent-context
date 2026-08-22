using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SourceAwareUsageLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_usage_sessions_SessionId",
                table: "usage");

            migrationBuilder.DropPrimaryKey(
                name: "PK_usage",
                table: "usage");

            migrationBuilder.DropIndex(
                name: "IX_usage_SessionId_Model",
                table: "usage");

            migrationBuilder.DropColumn(
                name: "Cost",
                table: "usage");

            migrationBuilder.RenameColumn(
                name: "Model",
                table: "usage",
                newName: "model");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "usage",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SessionId",
                table: "usage",
                newName: "session_id");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "usage",
                newName: "created_at_utc");

            migrationBuilder.RenameColumn(
                name: "TokensOut",
                table: "usage",
                newName: "output_tokens");

            migrationBuilder.RenameColumn(
                name: "TokensIn",
                table: "usage",
                newName: "input_tokens");

            migrationBuilder.AlterColumn<string>(
                name: "model",
                table: "usage",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<Guid>(
                name: "session_id",
                table: "usage",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "cached_input_tokens",
                table: "usage",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "capability",
                table: "usage",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "inference_route_id",
                table: "usage",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source",
                table: "usage",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "reported_session");

            migrationBuilder.AddPrimaryKey(
                name: "pk_usage",
                table: "usage",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_usage_inference_route_id",
                table: "usage",
                column: "inference_route_id");

            migrationBuilder.CreateIndex(
                name: "ix_usage_session_id_created_at_utc",
                table: "usage",
                columns: new[] { "session_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_usage_source_created_at_utc",
                table: "usage",
                columns: new[] { "source", "created_at_utc" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_cached_input_subset",
                table: "usage",
                sql: "cached_input_tokens <= input_tokens");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_capability_valid",
                table: "usage",
                sql: "capability IS NULL OR capability IN ('Chat', 'Embedding')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_source_relationships",
                table: "usage",
                sql: "(source = 'reported_session' AND session_id IS NOT NULL AND inference_route_id IS NULL AND capability IS NULL) OR source = 'learning_engine'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_source_valid",
                table: "usage",
                sql: "source IN ('reported_session', 'learning_engine')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_usage_tokens_non_negative",
                table: "usage",
                sql: "input_tokens >= 0 AND cached_input_tokens >= 0 AND output_tokens >= 0");

            migrationBuilder.AddForeignKey(
                name: "fk_usage_inference_routes_inference_route_id",
                table: "usage",
                column: "inference_route_id",
                principalTable: "inference_routes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_usage_sessions_session_id",
                table: "usage",
                column: "session_id",
                principalTable: "sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_usage_inference_routes_inference_route_id",
                table: "usage");

            migrationBuilder.DropForeignKey(
                name: "fk_usage_sessions_session_id",
                table: "usage");

            migrationBuilder.DropPrimaryKey(
                name: "pk_usage",
                table: "usage");

            migrationBuilder.DropIndex(
                name: "ix_usage_inference_route_id",
                table: "usage");

            migrationBuilder.DropIndex(
                name: "ix_usage_session_id_created_at_utc",
                table: "usage");

            migrationBuilder.DropIndex(
                name: "ix_usage_source_created_at_utc",
                table: "usage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_cached_input_subset",
                table: "usage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_capability_valid",
                table: "usage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_source_relationships",
                table: "usage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_source_valid",
                table: "usage");

            migrationBuilder.DropCheckConstraint(
                name: "ck_usage_tokens_non_negative",
                table: "usage");

            migrationBuilder.DropColumn(
                name: "cached_input_tokens",
                table: "usage");

            migrationBuilder.DropColumn(
                name: "capability",
                table: "usage");

            migrationBuilder.DropColumn(
                name: "inference_route_id",
                table: "usage");

            migrationBuilder.DropColumn(
                name: "source",
                table: "usage");

            migrationBuilder.RenameColumn(
                name: "model",
                table: "usage",
                newName: "Model");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "usage",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "session_id",
                table: "usage",
                newName: "SessionId");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "usage",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "output_tokens",
                table: "usage",
                newName: "TokensOut");

            migrationBuilder.RenameColumn(
                name: "input_tokens",
                table: "usage",
                newName: "TokensIn");

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "usage",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                table: "usage",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Cost",
                table: "usage",
                type: "numeric(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddPrimaryKey(
                name: "PK_usage",
                table: "usage",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_usage_SessionId_Model",
                table: "usage",
                columns: new[] { "SessionId", "Model" });

            migrationBuilder.AddForeignKey(
                name: "FK_usage_sessions_SessionId",
                table: "usage",
                column: "SessionId",
                principalTable: "sessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
