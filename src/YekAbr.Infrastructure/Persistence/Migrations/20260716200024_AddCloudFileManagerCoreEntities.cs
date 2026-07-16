using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YekAbr.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudFileManagerCoreEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConnectedCloudAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    AccountEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AccessToken = table.Column<string>(type: "text", nullable: false),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    AccessTokenExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RootFolderId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSyncedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConnectedCloudAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConnectedCloudAccounts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CloudTransferJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SourceConnectedCloudAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationConnectedCloudAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SourceItemName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SourceItemType = table.Column<int>(type: "integer", nullable: false),
                    DestinationParentFolderId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProgressPercentage = table.Column<int>(type: "integer", nullable: false),
                    TotalBytes = table.Column<long>(type: "bigint", nullable: true),
                    TransferredBytes = table.Column<long>(type: "bigint", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudTransferJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CloudTransferJobs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CloudTransferJobs_ConnectedCloudAccounts_DestinationConnect~",
                        column: x => x.DestinationConnectedCloudAccountId,
                        principalTable: "ConnectedCloudAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CloudTransferJobs_ConnectedCloudAccounts_SourceConnectedClo~",
                        column: x => x.SourceConnectedCloudAccountId,
                        principalTable: "ConnectedCloudAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CloudTransferJobs_DestinationConnectedCloudAccountId",
                table: "CloudTransferJobs",
                column: "DestinationConnectedCloudAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CloudTransferJobs_SourceConnectedCloudAccountId",
                table: "CloudTransferJobs",
                column: "SourceConnectedCloudAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CloudTransferJobs_Status",
                table: "CloudTransferJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CloudTransferJobs_UserId",
                table: "CloudTransferJobs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedCloudAccounts_Provider",
                table: "ConnectedCloudAccounts",
                column: "Provider");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedCloudAccounts_UserId",
                table: "ConnectedCloudAccounts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConnectedCloudAccounts_UserId_Provider_ProviderAccountId",
                table: "ConnectedCloudAccounts",
                columns: new[] { "UserId", "Provider", "ProviderAccountId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudTransferJobs");

            migrationBuilder.DropTable(
                name: "ConnectedCloudAccounts");
        }
    }
}
