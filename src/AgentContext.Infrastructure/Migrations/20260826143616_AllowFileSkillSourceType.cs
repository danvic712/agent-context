using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AllowFileSkillSourceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_skills_source_type",
                table: "skills");

            migrationBuilder.AddCheckConstraint(
                name: "ck_skills_source_type",
                table: "skills",
                sql: "source_type IS NULL OR source_type IN ('manual', 'file', 'zip', 'skills_sh', 'local_copy')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_skills_source_type",
                table: "skills");

            migrationBuilder.AddCheckConstraint(
                name: "ck_skills_source_type",
                table: "skills",
                sql: "source_type IS NULL OR source_type IN ('manual', 'zip', 'skills_sh', 'local_copy')");
        }
    }
}
