using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace STOKIO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderFulfillmentState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReturnedQuantity",
                table: "SalesOrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ShippedQuantity",
                table: "SalesOrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "SalesOrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE "SalesOrderItems" AS soi
                SET "ShippedQuantity" = soi."Quantity"
                FROM "SalesOrders" AS so
                WHERE soi."SalesOrderId" = so."Id"
                  AND so."Status" IN ('Shipped', 'Completed')
                  AND soi."ShippedQuantity" = 0;
                """);

            migrationBuilder.Sql("""UPDATE "SalesOrders" SET "Status" = 'Pending' WHERE "Status" = 'Preparing';""");
            migrationBuilder.Sql("""UPDATE "SalesOrders" SET "Status" = 'Shipped' WHERE "Status" = 'Completed';""");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_order_items_returned_quantity_non_negative",
                table: "SalesOrderItems",
                sql: "\"ReturnedQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_order_items_returned_quantity_not_over_shipped",
                table: "SalesOrderItems",
                sql: "\"ReturnedQuantity\" <= \"ShippedQuantity\"");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_order_items_shipped_quantity_non_negative",
                table: "SalesOrderItems",
                sql: "\"ShippedQuantity\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_sales_order_items_shipped_quantity_not_over_ordered",
                table: "SalesOrderItems",
                sql: "\"ShippedQuantity\" <= \"Quantity\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "SalesOrders" SET "Status" = 'Preparing' WHERE "Status" IN ('Draft', 'Pending', 'PartiallyShipped');""");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_order_items_returned_quantity_non_negative",
                table: "SalesOrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_order_items_returned_quantity_not_over_shipped",
                table: "SalesOrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_order_items_shipped_quantity_non_negative",
                table: "SalesOrderItems");

            migrationBuilder.DropCheckConstraint(
                name: "ck_sales_order_items_shipped_quantity_not_over_ordered",
                table: "SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "ReturnedQuantity",
                table: "SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "ShippedQuantity",
                table: "SalesOrderItems");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "SalesOrderItems");
        }
    }
}
