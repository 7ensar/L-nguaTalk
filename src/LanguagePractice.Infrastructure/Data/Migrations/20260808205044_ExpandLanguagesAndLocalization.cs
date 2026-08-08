using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace LanguagePractice.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExpandLanguagesAndLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Languages",
                columns: new[] { "Id", "Code", "IsActive", "Name", "NativeName" },
                values: new object[,]
                {
                    { 9, "zh", true, "Chinese", "中文" },
                    { 10, "hi", true, "Hindi", "हिन्दी" },
                    { 11, "ar", true, "Arabic", "العربية" },
                    { 12, "bn", true, "Bengali", "বাংলা" },
                    { 13, "pt", true, "Portuguese", "Português" },
                    { 14, "ru", true, "Russian", "Русский" },
                    { 15, "ur", true, "Urdu", "اردو" },
                    { 16, "id", true, "Indonesian", "Bahasa Indonesia" },
                    { 17, "sw", true, "Swahili", "Kiswahili" },
                    { 18, "vi", true, "Vietnamese", "Tiếng Việt" },
                    { 19, "pl", true, "Polish", "Polski" },
                    { 20, "nl", true, "Dutch", "Nederlands" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Languages",
                keyColumn: "Id",
                keyValue: 20);
        }
    }
}
