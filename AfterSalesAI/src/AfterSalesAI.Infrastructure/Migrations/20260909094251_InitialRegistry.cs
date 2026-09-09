using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace AfterSalesAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_applications_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_sources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Protocol = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ConfigurationReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_sources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_integration_sources_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_integration_sources_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DocumentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_documents_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tool_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IntegrationSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    InputSchemaJson = table.Column<string>(type: "jsonb", nullable: false),
                    OutputSchemaJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tool_definitions_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tool_definitions_integration_sources_IntegrationSourceId",
                        column: x => x.IntegrationSourceId,
                        principalTable: "integration_sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tool_definitions_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_knowledge_chunks_applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_knowledge_chunks_knowledge_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "knowledge_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_knowledge_chunks_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "tenants",
                columns: new[] { "Id", "IsActive", "Name", "Region" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000001"), true, "Demo After-Sales Tenant", "EU" });

            migrationBuilder.InsertData(
                table: "applications",
                columns: new[] { "Id", "Description", "IsActive", "Name", "TenantId" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000001"), "Demonstration application for after-sales capability registration.", true, "Demo After-Sales Portal", new Guid("00000000-0000-0000-0000-000000000001") });

            migrationBuilder.InsertData(
                table: "integration_sources",
                columns: new[] { "Id", "ApplicationId", "ConfigurationReference", "IsActive", "IsReadOnly", "Name", "Protocol", "TenantId", "Type" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), "IntegrationSources__DemoOrderApi", true, true, "Demo Order API", "REST", new Guid("00000000-0000-0000-0000-000000000001"), 1 },
                    { new Guid("30000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000001"), "IntegrationSources__DemoKnowledgeDatabase", true, true, "Demo Knowledge Database", "SQLServer", new Guid("00000000-0000-0000-0000-000000000001"), 2 }
                });

            migrationBuilder.InsertData(
                table: "tool_definitions",
                columns: new[] { "Id", "ApplicationId", "Description", "InputSchemaJson", "IntegrationSourceId", "IsActive", "Name", "OutputSchemaJson", "TenantId" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new Guid("20000000-0000-0000-0000-000000000001"), "Retrieve the current status of an order from an approved live system.", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", new Guid("30000000-0000-0000-0000-000000000001"), true, "get_order_status", "{\"type\":\"object\"}", new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new Guid("20000000-0000-0000-0000-000000000001"), "Retrieve the current delivery status for an order.", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", new Guid("30000000-0000-0000-0000-000000000001"), true, "get_delivery_status", "{\"type\":\"object\"}", new Guid("00000000-0000-0000-0000-000000000001") },
                    { new Guid("10000000-0000-0000-0000-000000000003"), new Guid("20000000-0000-0000-0000-000000000001"), "Search approved SOP and business process knowledge.", "{\"type\":\"object\",\"properties\":{\"query\":{\"type\":\"string\"}},\"required\":[\"query\"]}", new Guid("30000000-0000-0000-0000-000000000002"), true, "search_sop", "{\"type\":\"object\"}", new Guid("00000000-0000-0000-0000-000000000001") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_applications_TenantId_Id",
                table: "applications",
                columns: new[] { "TenantId", "Id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_applications_TenantId_Name",
                table: "applications",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_sources_ApplicationId",
                table: "integration_sources",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_integration_sources_TenantId_ApplicationId",
                table: "integration_sources",
                columns: new[] { "TenantId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_integration_sources_TenantId_ApplicationId_Name",
                table: "integration_sources",
                columns: new[] { "TenantId", "ApplicationId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_ApplicationId",
                table: "knowledge_chunks",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_DocumentId",
                table: "knowledge_chunks",
                column: "DocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_TenantId_ApplicationId_DocumentId",
                table: "knowledge_chunks",
                columns: new[] { "TenantId", "ApplicationId", "DocumentId" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_ApplicationId",
                table: "knowledge_documents",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_TenantId_ApplicationId_Name_Version",
                table: "knowledge_documents",
                columns: new[] { "TenantId", "ApplicationId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Name",
                table: "tenants",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tool_definitions_ApplicationId",
                table: "tool_definitions",
                column: "ApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_definitions_IntegrationSourceId",
                table: "tool_definitions",
                column: "IntegrationSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_tool_definitions_TenantId_ApplicationId_IsActive",
                table: "tool_definitions",
                columns: new[] { "TenantId", "ApplicationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_tool_definitions_TenantId_ApplicationId_Name",
                table: "tool_definitions",
                columns: new[] { "TenantId", "ApplicationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_chunks");

            migrationBuilder.DropTable(
                name: "tool_definitions");

            migrationBuilder.DropTable(
                name: "knowledge_documents");

            migrationBuilder.DropTable(
                name: "integration_sources");

            migrationBuilder.DropTable(
                name: "applications");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
