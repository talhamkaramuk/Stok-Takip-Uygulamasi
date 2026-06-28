using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STOKIO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurchasePartialReceive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReceivedQuantity",
                table: "PurchaseRequestItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "PurchaseRequestItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE "PurchaseRequestItems" AS pri
                SET "ReceivedQuantity" = pri."Quantity"
                FROM "PurchaseRequests" AS pr
                WHERE pri."PurchaseRequestId" = pr."Id"
                  AND pr."Status" = 'Received'
                  AND pri."ReceivedQuantity" = 0;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_purchase_request_items_received_quantity_non_negative",
                table: "PurchaseRequestItems",
                sql: "\"ReceivedQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_purchase_request_items_received_quantity_not_over_requested",
                table: "PurchaseRequestItems",
                sql: "\"ReceivedQuantity\" <= \"Quantity\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "PurchaseRequests" SET "Status" = 'Approved' WHERE "Status" = 'PartiallyReceived';""");

            migrationBuilder.DropCheckConstraint(
                name: "ck_purchase_request_items_received_quantity_non_negative",
                table: "PurchaseRequestItems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_purchase_request_items_received_quantity_not_over_requested",
                table: "PurchaseRequestItems");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "PurchaseRequestItems");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "PurchaseRequestItems");
        }
    }
}
