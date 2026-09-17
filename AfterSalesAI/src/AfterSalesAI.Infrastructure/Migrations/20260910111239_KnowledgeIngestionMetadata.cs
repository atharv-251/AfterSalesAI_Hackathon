using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AfterSalesAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeIngestionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_knowledge_documents_TenantId_ApplicationId_Name_Version",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_DocumentId",
                table: "knowledge_chunks");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedUtc",
                table: "knowledge_documents",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "SourcePath",
                table: "knowledge_documents",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedUtc",
                table: "knowledge_documents",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<int>(
                name: "ChunkIndex",
                table: "knowledge_chunks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedUtc",
                table: "knowledge_chunks",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_TenantId_ApplicationId_SourcePath",
                table: "knowledge_documents",
                columns: new[] { "TenantId", "ApplicationId", "SourcePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_DocumentId_ChunkIndex",
                table: "knowledge_chunks",
                columns: new[] { "DocumentId", "ChunkIndex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_knowledge_documents_TenantId_ApplicationId_SourcePath",
                table: "knowledge_documents");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_DocumentId_ChunkIndex",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "SourcePath",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "UpdatedUtc",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "ChunkIndex",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "CreatedUtc",
                table: "knowledge_chunks");

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_documents_TenantId_ApplicationId_Name_Version",
                table: "knowledge_documents",
                columns: new[] { "TenantId", "ApplicationId", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_DocumentId",
                table: "knowledge_chunks",
                column: "DocumentId");
        }
    }
}
