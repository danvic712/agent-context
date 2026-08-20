using Microsoft.EntityFrameworkCore.Migrations;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace AgentContext.Infrastructure.Migrations;

[DbContext(typeof(AgentContextDbContext))]
[Migration("20260820120000_SeedDefaultInferenceProviders")]
public partial class SeedDefaultInferenceProviders : Migration
{
    private const string OpenAiProviderId = "019a1b2c-3d4e-7f80-9123-456789abcdef";
    private const string DeepSeekProviderId = "019a1b2c-3d4e-7f81-9123-456789abcdef";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            INSERT INTO inference_providers
                (id, name, provider_type, base_url, api_key_secret_ref, created_at_utc, updated_at_utc)
            SELECT seed.id, seed.name, seed.provider_type, seed.base_url, seed.api_key_secret_ref,
                   CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
            FROM (VALUES
                ('019a1b2c-3d4e-7f80-9123-456789abcdef'::uuid, 'OpenAI', 'openai', 'https://api.openai.com/v1', ''),
                ('019a1b2c-3d4e-7f81-9123-456789abcdef'::uuid, 'DeepSeek', 'openai-compatible', 'https://api.deepseek.com/v1', '')
            ) AS seed(id, name, provider_type, base_url, api_key_secret_ref)
            WHERE NOT EXISTS (
                SELECT 1
                FROM inference_providers existing
                WHERE LOWER(existing.name) = LOWER(seed.name)
            )
            ON CONFLICT (id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            DELETE FROM inference_providers
            WHERE id IN ('{OpenAiProviderId}'::uuid, '{DeepSeekProviderId}'::uuid);
            """);
    }
}
