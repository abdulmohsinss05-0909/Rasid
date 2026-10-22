using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Rasid.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostMediaAndCursorSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CursorSource",
                schema: "rasid",
                table: "Topics",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaAltText",
                schema: "rasid",
                table: "Posts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaKind",
                schema: "rasid",
                table: "Posts",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaPreviewUrl",
                schema: "rasid",
                table: "Posts",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CursorSource",
                schema: "rasid",
                table: "Topics");

            migrationBuilder.DropColumn(
                name: "MediaAltText",
                schema: "rasid",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "MediaKind",
                schema: "rasid",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "MediaPreviewUrl",
                schema: "rasid",
                table: "Posts");
        }
    }
}
