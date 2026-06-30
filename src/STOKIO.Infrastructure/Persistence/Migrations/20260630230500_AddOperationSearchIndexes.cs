using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using STOKIO.Infrastructure.Persistence;

#nullable disable

namespace STOKIO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(StokioDbContext))]
    [Migration("20260630230500_AddOperationSearchIndexes")]
    public partial class AddOperationSearchIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "Shipments",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "SalesOrders",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "ReturnRequests",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SearchText",
                table: "PurchaseRequests",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "SalesOrders" AS sales
                SET "SearchText" = left(trim(regexp_replace(lower(concat_ws(' ',
                    sales."OrderNumber",
                    sales."CustomerName",
                    (SELECT warehouse."Name" FROM "Warehouses" AS warehouse WHERE warehouse."Id" = sales."WarehouseId")
                )), '\s+', ' ', 'g')), 2048);

                UPDATE "PurchaseRequests" AS purchase
                SET "SearchText" = left(trim(regexp_replace(lower(concat_ws(' ',
                    purchase."RequestNumber",
                    purchase."SupplierName",
                    (SELECT warehouse."Name" FROM "Warehouses" AS warehouse WHERE warehouse."Id" = purchase."WarehouseId")
                )), '\s+', ' ', 'g')), 2048);

                UPDATE "Shipments" AS shipment
                SET "SearchText" = left(trim(regexp_replace(lower(concat_ws(' ',
                    shipment."ShipmentNumber",
                    shipment."RecipientName",
                    shipment."TrackingNumber",
                    (SELECT warehouse."Name" FROM "Warehouses" AS warehouse WHERE warehouse."Id" = shipment."WarehouseId"),
                    (SELECT sales."OrderNumber" FROM "SalesOrders" AS sales WHERE sales."Id" = shipment."SalesOrderId")
                )), '\s+', ' ', 'g')), 2048);

                UPDATE "ReturnRequests" AS return_request
                SET "SearchText" = left(trim(regexp_replace(lower(concat_ws(' ',
                    return_request."ReturnNumber",
                    return_request."CustomerName",
                    return_request."Reason",
                    (SELECT warehouse."Name" FROM "Warehouses" AS warehouse WHERE warehouse."Id" = return_request."WarehouseId"),
                    (SELECT sales."OrderNumber" FROM "SalesOrders" AS sales WHERE sales."Id" = return_request."SalesOrderId")
                )), '\s+', ' ', 'g')), 2048);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TenantId_CreatedAt",
                table: "Shipments",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_TenantId_Status_CreatedAt",
                table: "Shipments",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_CreatedAt",
                table: "SalesOrders",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SalesOrders_TenantId_Status_CreatedAt",
                table: "SalesOrders",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_TenantId_CreatedAt",
                table: "ReturnRequests",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ReturnRequests_TenantId_Status_CreatedAt",
                table: "ReturnRequests",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_TenantId_CreatedAt",
                table: "PurchaseRequests",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_TenantId_Status_CreatedAt",
                table: "PurchaseRequests",
                columns: new[] { "TenantId", "Status", "CreatedAt" });

            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pg_trgm;
                CREATE INDEX IF NOT EXISTS "IX_SalesOrders_SearchText_Trgm" ON "SalesOrders" USING gin ("SearchText" gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS "IX_PurchaseRequests_SearchText_Trgm" ON "PurchaseRequests" USING gin ("SearchText" gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS "IX_Shipments_SearchText_Trgm" ON "Shipments" USING gin ("SearchText" gin_trgm_ops);
                CREATE INDEX IF NOT EXISTS "IX_ReturnRequests_SearchText_Trgm" ON "ReturnRequests" USING gin ("SearchText" gin_trgm_ops);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_ReturnRequests_SearchText_Trgm";
                DROP INDEX IF EXISTS "IX_Shipments_SearchText_Trgm";
                DROP INDEX IF EXISTS "IX_PurchaseRequests_SearchText_Trgm";
                DROP INDEX IF EXISTS "IX_SalesOrders_SearchText_Trgm";
                """);

            migrationBuilder.DropIndex(
                name: "IX_Shipments_TenantId_CreatedAt",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_TenantId_Status_CreatedAt",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TenantId_CreatedAt",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_SalesOrders_TenantId_Status_CreatedAt",
                table: "SalesOrders");

            migrationBuilder.DropIndex(
                name: "IX_ReturnRequests_TenantId_CreatedAt",
                table: "ReturnRequests");

            migrationBuilder.DropIndex(
                name: "IX_ReturnRequests_TenantId_Status_CreatedAt",
                table: "ReturnRequests");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequests_TenantId_CreatedAt",
                table: "PurchaseRequests");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseRequests_TenantId_Status_CreatedAt",
                table: "PurchaseRequests");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "ReturnRequests");

            migrationBuilder.DropColumn(
                name: "SearchText",
                table: "PurchaseRequests");
        }
    }
}
