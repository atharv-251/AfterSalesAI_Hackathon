using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterSalesAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ImportDemoDataset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "demo");

            migrationBuilder.CreateTable(
                name: "Bom",
                schema: "demo",
                columns: table => new
                {
                    BomId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssemblyPartNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ComponentPartNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    QuantityPer = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bom", x => x.BomId);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                schema: "demo",
                columns: table => new
                {
                    ClaimId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DealerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PoNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimQuantity = table.Column<int>(type: "int", nullable: false),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ClaimDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClaimAmountEur = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ResolutionCode = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.ClaimId);
                });

            migrationBuilder.CreateTable(
                name: "Dealers",
                schema: "demo",
                columns: table => new
                {
                    DealerId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DealerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ContactEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreditLimitEur = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OnboardedDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dealers", x => x.DealerId);
                });

            migrationBuilder.CreateTable(
                name: "Inventory",
                schema: "demo",
                columns: table => new
                {
                    InventoryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WarehouseLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OnHandQuantity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false),
                    AvailableQuantity = table.Column<int>(type: "int", nullable: false),
                    ReorderPoint = table.Column<int>(type: "int", nullable: false),
                    BinLocation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastCountDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventory", x => x.InventoryId);
                });

            migrationBuilder.CreateTable(
                name: "Knowledge",
                schema: "demo",
                columns: table => new
                {
                    DocumentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Module = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastUpdated = table.Column<DateOnly>(type: "date", nullable: false),
                    OwnerTeam = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Knowledge", x => x.DocumentId);
                });

            migrationBuilder.CreateTable(
                name: "Parts",
                schema: "demo",
                columns: table => new
                {
                    PartNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PartName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitPriceEur = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    WarrantyMonths = table.Column<int>(type: "int", nullable: false),
                    SupplierId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HazmatFlag = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LeadTimeDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.PartNo);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                schema: "demo",
                columns: table => new
                {
                    PoNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PoLineNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DealerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PartNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    OrderQuantity = table.Column<int>(type: "int", nullable: false),
                    UnitPriceEur = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotalEur = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShipmentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => new { x.PoNo, x.PoLineNo });
                });

            migrationBuilder.CreateTable(
                name: "Shipments",
                schema: "demo",
                columns: table => new
                {
                    ShipmentId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PoNo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DealerId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Carrier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ShipDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EstimatedDeliveryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualDeliveryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    TrackingNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipments", x => x.ShipmentId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Bom_AssemblyPartNo",
                schema: "demo",
                table: "Bom",
                column: "AssemblyPartNo");

            migrationBuilder.CreateIndex(
                name: "IX_Bom_ComponentPartNo",
                schema: "demo",
                table: "Bom",
                column: "ComponentPartNo");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_DealerId",
                schema: "demo",
                table: "Claims",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PartNo",
                schema: "demo",
                table: "Claims",
                column: "PartNo");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PoNo",
                schema: "demo",
                table: "Claims",
                column: "PoNo");

            migrationBuilder.CreateIndex(
                name: "IX_Dealers_Status",
                schema: "demo",
                table: "Dealers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_PartNo",
                schema: "demo",
                table: "Inventory",
                column: "PartNo");

            migrationBuilder.CreateIndex(
                name: "IX_Knowledge_ErrorCode",
                schema: "demo",
                table: "Knowledge",
                column: "ErrorCode");

            migrationBuilder.CreateIndex(
                name: "IX_Knowledge_Module",
                schema: "demo",
                table: "Knowledge",
                column: "Module");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_Status",
                schema: "demo",
                table: "Parts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_DealerId",
                schema: "demo",
                table: "PurchaseOrders",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_PartNo",
                schema: "demo",
                table: "PurchaseOrders",
                column: "PartNo");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ShipmentId",
                schema: "demo",
                table: "PurchaseOrders",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_DealerId",
                schema: "demo",
                table: "Shipments",
                column: "DealerId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_PoNo",
                schema: "demo",
                table: "Shipments",
                column: "PoNo");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Status",
                schema: "demo",
                table: "Shipments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Bom",
                schema: "demo");

            migrationBuilder.DropTable(
                name: "Claims",
                schema: "demo");

            migrationBuilder.DropTable(
                name: "Dealers",
                schema: "demo");

            migrationBuilder.DropTable(
                name: "Inventory",
                schema: "demo");

            migrationBuilder.DropTable(
                name: "Knowledge",
                schema: "demo");

            migrationBuilder.DropTable(
                name: "Parts",
                schema: "demo");

            migrationBuilder.DropTable(
                name: "PurchaseOrders",
                schema: "demo");

            migrationBuilder.DropTable(
                name: "Shipments",
                schema: "demo");
        }
    }
}
