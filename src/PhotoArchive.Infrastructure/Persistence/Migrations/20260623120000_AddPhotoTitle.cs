using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoArchive.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PhotoArchiveDbContext))]
    [Migration("20260623120000_AddPhotoTitle")]
    public partial class AddPhotoTitle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "PhotoMetadata",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "PhotoMetadata");
        }
    }
}
