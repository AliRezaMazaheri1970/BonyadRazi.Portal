using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserActionLogCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserActionLogs_ActionType_CompanyCode_Utc",
                table: "UserActionLogs",
                columns: new[] { "ActionType", "CompanyCode", "Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActionLogs_CompanyCode_Utc",
                table: "UserActionLogs",
                columns: new[] { "CompanyCode", "Utc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserActionLogs_StatusCode_Utc",
                table: "UserActionLogs",
                columns: new[] { "StatusCode", "Utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserActionLogs_ActionType_CompanyCode_Utc",
                table: "UserActionLogs");

            migrationBuilder.DropIndex(
                name: "IX_UserActionLogs_CompanyCode_Utc",
                table: "UserActionLogs");

            migrationBuilder.DropIndex(
                name: "IX_UserActionLogs_StatusCode_Utc",
                table: "UserActionLogs");
        }
    }
}
