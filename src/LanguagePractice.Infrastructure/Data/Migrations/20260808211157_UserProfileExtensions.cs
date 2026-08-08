using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguagePractice.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserProfileExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Interests",
                table: "Profiles",
                type: "TEXT",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LanguageLevel",
                table: "Profiles",
                type: "TEXT",
                maxLength: 8,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Interests",
                table: "Profiles");

            migrationBuilder.DropColumn(
                name: "LanguageLevel",
                table: "Profiles");
        }
    }
}
