using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finance.Application.Infrastructure.Storage.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", nullable: false),
                    currency = table.Column<string>(type: "TEXT", maxLength: 3, nullable: false),
                    opening_balance = table.Column<long>(type: "INTEGER", nullable: false),
                    opened_on = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    excluded_from_totals = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_closed = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    synced_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    external_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    parent_key = table.Column<string>(type: "TEXT", nullable: true),
                    kind = table.Column<string>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    icon = table.Column<string>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", nullable: false),
                    exclude_from_reports = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    synced_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    external_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "places",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    synced_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    external_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_places", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "settings",
                columns: table => new
                {
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settings", x => x.name);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    key = table.Column<string>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", nullable: false),
                    source_account_key = table.Column<string>(type: "TEXT", nullable: false),
                    target_account_key = table.Column<string>(type: "TEXT", nullable: true),
                    amount = table.Column<long>(type: "INTEGER", nullable: false),
                    target_amount = table.Column<long>(type: "INTEGER", nullable: true),
                    category_key = table.Column<string>(type: "TEXT", nullable: true),
                    place_key = table.Column<string>(type: "TEXT", nullable: true),
                    occurred_on = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    synced_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    external_id = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions", x => x.key);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_sort_order",
                table: "accounts",
                column: "sort_order");

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_key",
                table: "categories",
                column: "parent_key");

            migrationBuilder.CreateIndex(
                name: "ix_places_name",
                table: "places",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_occurred_on",
                table: "transactions",
                columns: new[] { "category_key", "occurred_on" },
                filter: "deleted_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_place_key",
                table: "transactions",
                column: "place_key",
                filter: "deleted_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_source_feed",
                table: "transactions",
                columns: new[] { "source_account_key", "occurred_on", "created_at_utc", "key" },
                descending: new[] { false, true, true, true },
                filter: "deleted_at_utc IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_target_feed",
                table: "transactions",
                columns: new[] { "target_account_key", "occurred_on", "created_at_utc", "key" },
                descending: new[] { false, true, true, true },
                filter: "deleted_at_utc IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "places");

            migrationBuilder.DropTable(
                name: "settings");

            migrationBuilder.DropTable(
                name: "transactions");
        }
    }
}
