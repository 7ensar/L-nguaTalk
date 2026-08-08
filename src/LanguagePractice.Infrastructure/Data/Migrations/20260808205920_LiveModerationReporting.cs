using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguagePractice.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class LiveModerationReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserReports_AspNetUsers_ReportedUserId",
                table: "UserReports");

            migrationBuilder.AlterColumn<string>(
                name: "ReportedUserId",
                table: "UserReports",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "AutoAction",
                table: "UserReports",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "UserReports",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReportedGuestSessionId",
                table: "UserReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedPeerDisplayName",
                table: "UserReports",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReportedPeerSocketId",
                table: "UserReports",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReporterGuestSessionId",
                table: "UserReports",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReporterIpHash",
                table: "UserReports",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomId",
                table: "UserReports",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BanExpiresAtUtc",
                table: "GuestSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BanReason",
                table: "GuestSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BannedAtUtc",
                table: "GuestSessions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBanned",
                table: "GuestSessions",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "BanRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    GuestSessionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PeerKey = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    BanType = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedByAdminId = table.Column<string>(type: "TEXT", nullable: true),
                    IsSystemGenerated = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    RelatedReportId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BanRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BanRecords_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BanRecords_GuestSessions_GuestSessionId",
                        column: x => x.GuestSessionId,
                        principalTable: "GuestSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BanRecords_UserReports_RelatedReportId",
                        column: x => x.RelatedReportId,
                        principalTable: "UserReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReportedGuestSessionId",
                table: "UserReports",
                column: "ReportedGuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_ReporterGuestSessionId",
                table: "UserReports",
                column: "ReporterGuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_UserReports_RoomId",
                table: "UserReports",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_BanRecords_ExpiresAtUtc",
                table: "BanRecords",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_BanRecords_GuestSessionId",
                table: "BanRecords",
                column: "GuestSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_BanRecords_IsActive",
                table: "BanRecords",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_BanRecords_PeerKey",
                table: "BanRecords",
                column: "PeerKey");

            migrationBuilder.CreateIndex(
                name: "IX_BanRecords_RelatedReportId",
                table: "BanRecords",
                column: "RelatedReportId");

            migrationBuilder.CreateIndex(
                name: "IX_BanRecords_UserId",
                table: "BanRecords",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserReports_AspNetUsers_ReportedUserId",
                table: "UserReports",
                column: "ReportedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReports_GuestSessions_ReportedGuestSessionId",
                table: "UserReports",
                column: "ReportedGuestSessionId",
                principalTable: "GuestSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReports_GuestSessions_ReporterGuestSessionId",
                table: "UserReports",
                column: "ReporterGuestSessionId",
                principalTable: "GuestSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserReports_AspNetUsers_ReportedUserId",
                table: "UserReports");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReports_GuestSessions_ReportedGuestSessionId",
                table: "UserReports");

            migrationBuilder.DropForeignKey(
                name: "FK_UserReports_GuestSessions_ReporterGuestSessionId",
                table: "UserReports");

            migrationBuilder.DropTable(
                name: "BanRecords");

            migrationBuilder.DropIndex(
                name: "IX_UserReports_ReportedGuestSessionId",
                table: "UserReports");

            migrationBuilder.DropIndex(
                name: "IX_UserReports_ReporterGuestSessionId",
                table: "UserReports");

            migrationBuilder.DropIndex(
                name: "IX_UserReports_RoomId",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "AutoAction",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReportedGuestSessionId",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReportedPeerDisplayName",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReportedPeerSocketId",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReporterGuestSessionId",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "ReporterIpHash",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "UserReports");

            migrationBuilder.DropColumn(
                name: "BanExpiresAtUtc",
                table: "GuestSessions");

            migrationBuilder.DropColumn(
                name: "BanReason",
                table: "GuestSessions");

            migrationBuilder.DropColumn(
                name: "BannedAtUtc",
                table: "GuestSessions");

            migrationBuilder.DropColumn(
                name: "IsBanned",
                table: "GuestSessions");

            migrationBuilder.AlterColumn<string>(
                name: "ReportedUserId",
                table: "UserReports",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserReports_AspNetUsers_ReportedUserId",
                table: "UserReports",
                column: "ReportedUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
