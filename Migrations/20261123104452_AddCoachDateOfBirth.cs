using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCoachDateOfBirth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfBirth",
                table: "Coaches",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "Coaches");
        }
    }
}
