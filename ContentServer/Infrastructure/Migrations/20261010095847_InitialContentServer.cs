using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentServer.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialContentServer : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Administrators",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                IsSuperAdministrator = table.Column<bool>(type: "INTEGER", nullable: false),
                Contact = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                ReviewMessage = table.Column<string>(type: "TEXT", nullable: true),
                ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Administrators", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Contents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                PublisherId = table.Column<Guid>(type: "TEXT", nullable: false),
                Type = table.Column<string>(type: "TEXT", nullable: false),
                Identifier = table.Column<string>(type: "TEXT", nullable: false),
                NormalizedIdentifier = table.Column<string>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Summary = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Contents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "GameAnnouncements",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                Title = table.Column<string>(type: "TEXT", nullable: false),
                Body = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameAnnouncements", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "GameReleases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                Version = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameReleases", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "PackageBlobs",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                Hash = table.Column<string>(type: "TEXT", nullable: false),
                BlobHash = table.Column<string>(type: "TEXT", nullable: false),
                Size = table.Column<long>(type: "INTEGER", nullable: false),
                FileName = table.Column<string>(type: "TEXT", nullable: false),
                MediaType = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PackageBlobs", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Publishers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                Contact = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                ReviewMessage = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Publishers", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "ReviewRecords",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                AdministratorId = table.Column<Guid>(type: "TEXT", nullable: false),
                TargetType = table.Column<string>(type: "TEXT", nullable: false),
                TargetId = table.Column<string>(type: "TEXT", nullable: false),
                Decision = table.Column<string>(type: "TEXT", nullable: false),
                Message = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReviewRecords", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AdministratorKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                AdministratorId = table.Column<Guid>(type: "TEXT", nullable: false),
                KeyPrefix = table.Column<string>(type: "TEXT", nullable: false),
                KeyHash = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AdministratorKeys", x => x.Id);
                table.ForeignKey(
                    name: "FK_AdministratorKeys_Administrators_AdministratorId",
                    column: x => x.AdministratorId,
                    principalTable: "Administrators",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "GameReleaseArtifacts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                GameReleaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                Platform = table.Column<string>(type: "TEXT", nullable: false),
                DownloadUrl = table.Column<string>(type: "TEXT", nullable: false),
                Sha256 = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_GameReleaseArtifacts", x => x.Id);
                table.ForeignKey(
                    name: "FK_GameReleaseArtifacts_GameReleases_GameReleaseId",
                    column: x => x.GameReleaseId,
                    principalTable: "GameReleases",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ContentVersions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                ContentId = table.Column<Guid>(type: "TEXT", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Version = table.Column<string>(type: "TEXT", nullable: false),
                Summary = table.Column<string>(type: "TEXT", nullable: false),
                Description = table.Column<string>(type: "TEXT", nullable: false),
                PublisherId = table.Column<Guid>(type: "TEXT", nullable: false),
                ContentType = table.Column<string>(type: "TEXT", nullable: false),
                Identifier = table.Column<string>(type: "TEXT", nullable: false),
                PackageHash = table.Column<string>(type: "TEXT", nullable: false),
                BlobHash = table.Column<string>(type: "TEXT", nullable: true),
                PackageBlobId = table.Column<Guid>(type: "TEXT", nullable: false),
                MetadataJson = table.Column<string>(type: "TEXT", nullable: true),
                Status = table.Column<string>(type: "TEXT", nullable: false),
                ReviewMessage = table.Column<string>(type: "TEXT", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                PublishedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ContentVersions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ContentVersions_Contents_ContentId",
                    column: x => x.ContentId,
                    principalTable: "Contents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ContentVersions_PackageBlobs_PackageBlobId",
                    column: x => x.PackageBlobId,
                    principalTable: "PackageBlobs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "DirectoryServers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
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

        migrationBuilder.CreateTable(
            name: "PublisherKeys",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                PublisherId = table.Column<Guid>(type: "TEXT", nullable: false),
                KeyPrefix = table.Column<string>(type: "TEXT", nullable: false),
                KeyHash = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                LastUsedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                RevokedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PublisherKeys", x => x.Id);
                table.ForeignKey(
                    name: "FK_PublisherKeys_Publishers_PublisherId",
                    column: x => x.PublisherId,
                    principalTable: "Publishers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ServerSources",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "TEXT", nullable: false),
                Deleted = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
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
            name: "IX_AdministratorKeys_AdministratorId",
            table: "AdministratorKeys",
            column: "AdministratorId");

        migrationBuilder.CreateIndex(
            name: "IX_AdministratorKeys_KeyHash",
            table: "AdministratorKeys",
            column: "KeyHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Contents_NormalizedIdentifier",
            table: "Contents",
            column: "NormalizedIdentifier",
            unique: true,
            filter: "\"Deleted\" = 0");

        migrationBuilder.CreateIndex(
            name: "IX_ContentVersions_ContentId_Version",
            table: "ContentVersions",
            columns: new[] { "ContentId", "Version" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ContentVersions_PackageBlobId",
            table: "ContentVersions",
            column: "PackageBlobId");

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

        migrationBuilder.CreateIndex(
            name: "IX_GameAnnouncements_Status_PublishedAt",
            table: "GameAnnouncements",
            columns: new[] { "Status", "PublishedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_GameReleaseArtifacts_GameReleaseId_Platform",
            table: "GameReleaseArtifacts",
            columns: new[] { "GameReleaseId", "Platform" },
            unique: true,
            filter: "\"Deleted\" = 0");

        migrationBuilder.CreateIndex(
            name: "IX_GameReleaseArtifacts_Platform",
            table: "GameReleaseArtifacts",
            column: "Platform");

        migrationBuilder.CreateIndex(
            name: "IX_GameReleases_Status",
            table: "GameReleases",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_GameReleases_Version",
            table: "GameReleases",
            column: "Version",
            unique: true,
            filter: "\"Deleted\" = 0");

        migrationBuilder.CreateIndex(
            name: "IX_PackageBlobs_Hash",
            table: "PackageBlobs",
            column: "Hash",
            unique: true,
            filter: "\"Deleted\" = 0");

        migrationBuilder.CreateIndex(
            name: "IX_PublisherKeys_KeyHash",
            table: "PublisherKeys",
            column: "KeyHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_PublisherKeys_PublisherId",
            table: "PublisherKeys",
            column: "PublisherId");

        migrationBuilder.CreateIndex(
            name: "IX_Publishers_Status",
            table: "Publishers",
            column: "Status");

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
            name: "AdministratorKeys");

        migrationBuilder.DropTable(
            name: "ContentVersions");

        migrationBuilder.DropTable(
            name: "DirectoryServers");

        migrationBuilder.DropTable(
            name: "GameAnnouncements");

        migrationBuilder.DropTable(
            name: "GameReleaseArtifacts");

        migrationBuilder.DropTable(
            name: "PublisherKeys");

        migrationBuilder.DropTable(
            name: "ReviewRecords");

        migrationBuilder.DropTable(
            name: "ServerSources");

        migrationBuilder.DropTable(
            name: "Administrators");

        migrationBuilder.DropTable(
            name: "Contents");

        migrationBuilder.DropTable(
            name: "PackageBlobs");

        migrationBuilder.DropTable(
            name: "GameReleases");

        migrationBuilder.DropTable(
            name: "Publishers");
    }
}
