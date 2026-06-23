using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoArchive.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(PhotoArchiveDbContext))]
    [Migration("20260622173000_AddVisualSimilarityMetadata")]
    public partial class AddVisualSimilarityMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AverageColorHex",
                table: "PhotoMetadata",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerceptualHash",
                table: "PhotoMetadata",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageColorHex",
                table: "PhotoMetadata");

            migrationBuilder.DropColumn(
                name: "PerceptualHash",
                table: "PhotoMetadata");
        }
    }
}
