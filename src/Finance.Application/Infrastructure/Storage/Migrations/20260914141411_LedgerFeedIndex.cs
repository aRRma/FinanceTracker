using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Application.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class LedgerFeedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_transactions_feed",
                table: "transactions",
                columns: new[] { "occurred_on", "created_at_utc", "key" },
                descending: new bool[0],
                filter: "deleted_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transactions_feed",
                table: "transactions");
        }
    }
}
