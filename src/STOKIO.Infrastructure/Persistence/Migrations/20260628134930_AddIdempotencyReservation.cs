using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STOKIO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "IdempotencyRecords",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                table: "IdempotencyRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now() + interval '24 hours'");

            migrationBuilder.AddColumn<string>(
                name: "ResponseSnapshotJson",
                table: "IdempotencyRecords",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "IdempotencyRecords",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Completed");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "ResponseSnapshotJson",
                table: "IdempotencyRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "IdempotencyRecords");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "IdempotencyRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }
    }
}
