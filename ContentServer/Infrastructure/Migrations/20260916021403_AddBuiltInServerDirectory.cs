using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentServer.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddBuiltInServerDirectory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "DirectoryServers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                PublisherId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Address = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                ReviewStatus = table.Column<string>(type: "TEXT", nullable: false),
                ReviewMessage = table.Column<string>(type: "TEXT", nullable: true),
                IsEnabledByPublisher = table.Column<bool>(type: "INTEGER", nullable: false),
                SuspendedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                SuspensionReason = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DirectoryServers", x => x.Id);
                table.ForeignKey(
                    name: "FK_DirectoryServers_Publishers_PublisherId",
                    column: x => x.PublisherId,
                    principalTable: "Publishers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_DirectoryServers_Address",
            table: "DirectoryServers",
            column: "Address");

        migrationBuilder.CreateIndex(
            name: "IX_DirectoryServers_PublisherId",
            table: "DirectoryServers",
            column: "PublisherId");

        migrationBuilder.CreateIndex(
            name: "IX_DirectoryServers_ReviewStatus",
            table: "DirectoryServers",
            column: "ReviewStatus");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "DirectoryServers");
    }
}
