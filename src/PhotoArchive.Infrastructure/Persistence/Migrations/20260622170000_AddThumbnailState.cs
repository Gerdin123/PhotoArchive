using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoArchive.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PhotoArchiveDbContext))]
    [Migration("20260622170000_AddThumbnailState")]
    public partial class AddThumbnailState : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                table: "ArchiveFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailStatus",
                table: "ArchiveFiles",
                type: "TEXT",
                nullable: false,
                defaultValue: "NotCreated");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                table: "ArchiveFiles");

            migrationBuilder.DropColumn(
                name: "ThumbnailStatus",
                table: "ArchiveFiles");
        }
    }
}
