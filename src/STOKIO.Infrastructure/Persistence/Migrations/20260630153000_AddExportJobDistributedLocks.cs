using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STOKIO.Infrastructure.Persistence;

#nullable disable

namespace STOKIO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StokioDbContext))]
    [Migration("20260630153000_AddExportJobDistributedLocks")]
    public partial class AddExportJobDistributedLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "ExportJobs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntil",
                table: "ExportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "ExportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRetryCount",
                table: "ExportJobs",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAttemptAt",
                table: "ExportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "ExportJobs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FailedReasonCode",
                table: "ExportJobs",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_Status_LockedUntil_CreatedAt",
                table: "ExportJobs",
                columns: new[] { "Status", "LockedUntil", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExportJobs_Status_NextAttemptAt_CreatedAt",
                table: "ExportJobs",
                columns: new[] { "Status", "NextAttemptAt", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExportJobs_Status_LockedUntil_CreatedAt",
                table: "ExportJobs");

            migrationBuilder.DropIndex(
                name: "IX_ExportJobs_Status_NextAttemptAt_CreatedAt",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "FailedReasonCode",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "LastAttemptAt",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "MaxRetryCount",
                table: "ExportJobs");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "ExportJobs");
        }
    }
}
