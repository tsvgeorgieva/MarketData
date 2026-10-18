using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketData.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandlesAndMonitoredSymbols : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candles",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    OpenTime = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(28,8)", precision: 28, scale: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => new { x.Symbol, x.OpenTime });
                });

            migrationBuilder.CreateTable(
                name: "MonitoredSymbols",
                columns: table => new
                {
                    Symbol = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    RemovedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonitoredSymbols", x => x.Symbol);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Candles");

            migrationBuilder.DropTable(
                name: "MonitoredSymbols");
        }
    }
}
