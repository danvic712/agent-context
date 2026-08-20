using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations;

[DbContext(typeof(AgentContextDbContext))]
[Migration("20260820130000_RecreateSettingsWithPostgresNames")]
public partial class RecreateSettingsWithPostgresNames : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Settings are platform preferences. The MVP explicitly allows this
        // table to be recreated without preserving existing values.
        migrationBuilder.DropTable(
            name: "settings");

        migrationBuilder.CreateTable(
            name: "settings",
            columns: table => new
            {
                key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_settings", x => x.key);
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "settings");

        migrationBuilder.CreateTable(
            name: "settings",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Value = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_settings", x => x.Key);
            });
    }
}
