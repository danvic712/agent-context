using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentContext.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInferenceConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inference_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inference_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inference_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    base_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    api_key_secret_ref = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inference_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inference_routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    inference_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capability = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inference_routes", x => x.id);
                    table.ForeignKey(
                        name: "fk_inference_routes_inference_configurations_inference_configuration_id",
                        column: x => x.inference_configuration_id,
                        principalTable: "inference_configurations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_inference_routes_inference_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "inference_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inference_routes_provider_id",
                table: "inference_routes",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "uq_inference_routes_inference_configuration_id_capability",
                table: "inference_routes",
                columns: new[] { "inference_configuration_id", "capability" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inference_routes");

            migrationBuilder.DropTable(
                name: "inference_configurations");

            migrationBuilder.DropTable(
                name: "inference_providers");
        }
    }
}
