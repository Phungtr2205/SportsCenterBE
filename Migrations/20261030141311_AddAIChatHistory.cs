using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SportsCenterAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAIChatHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phiên bản trước đã tự thêm PackageType lúc startup; giữ nguyên cột nếu DB đã có.
            migrationBuilder.Sql("""
                ALTER TABLE "MembershipPackages"
                ADD COLUMN IF NOT EXISTS "PackageType" character varying(20) NOT NULL DEFAULT 'Standard';
                """);

            migrationBuilder.CreateTable(
                name: "AIChatHistories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Answer = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIChatHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIChatHistories_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AIChatHistories_UserId_Id",
                table: "AIChatHistories",
                columns: new[] { "UserId", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AIChatHistories");

            // Không xóa PackageType vì cột này có thể đã tồn tại trước migration lịch sử chat.
        }
    }
}
