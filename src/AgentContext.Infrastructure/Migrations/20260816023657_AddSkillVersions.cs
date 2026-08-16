using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_skills_WorkspaceId_DomainId_Slug",
                table: "skills");

            migrationBuilder.CreateIndex(
                name: "IX_skills_WorkspaceId_DomainId_Slug",
                table: "skills",
                columns: new[] { "WorkspaceId", "DomainId", "Slug" });

            migrationBuilder.CreateIndex(
                name: "IX_skills_WorkspaceId_DomainId_Slug_Version",
                table: "skills",
                columns: new[] { "WorkspaceId", "DomainId", "Slug", "Version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_skills_WorkspaceId_DomainId_Slug",
                table: "skills");

            migrationBuilder.DropIndex(
                name: "IX_skills_WorkspaceId_DomainId_Slug_Version",
                table: "skills");

            migrationBuilder.CreateIndex(
                name: "IX_skills_WorkspaceId_DomainId_Slug",
                table: "skills",
                columns: new[] { "WorkspaceId", "DomainId", "Slug" },
                unique: true);
        }
    }
}
