using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentServer.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddServerSources : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ServerSources",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                ApiUrl = table.Column<string>(type: "TEXT", nullable: false),
                PublisherId = table.Column<Guid>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                ReviewMessage = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServerSources", x => x.Id);
                table.ForeignKey(
                    name: "FK_ServerSources_Publishers_PublisherId",
                    column: x => x.PublisherId,
                    principalTable: "Publishers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ServerSources_ApiUrl",
            table: "ServerSources",
            column: "ApiUrl");

        migrationBuilder.CreateIndex(
            name: "IX_ServerSources_PublisherId",
            table: "ServerSources",
            column: "PublisherId");

        migrationBuilder.CreateIndex(
            name: "IX_ServerSources_Status",
            table: "ServerSources",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ServerSources");
    }
}
