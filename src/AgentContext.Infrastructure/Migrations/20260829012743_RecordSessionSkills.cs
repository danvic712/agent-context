using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecordSessionSkills : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "skills_used",
                table: "sessions",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "skills_used",
                table: "sessions");
        }
    }
}
