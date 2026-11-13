using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SportsCenterAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddClassImageUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "SportClasses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "SportClasses");
        }
    }
}
