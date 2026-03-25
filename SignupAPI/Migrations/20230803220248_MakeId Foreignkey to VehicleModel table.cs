using Microsoft.EntityFrameworkCore.Migrations;

namespace SignupAPI.Migrations
{
    public partial class MakeIdForeignkeytoVehicleModeltable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Make",
                table: "VehicleModel");

            migrationBuilder.AddColumn<int>(
                name: "MakeId",
                table: "VehicleModel",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MakeId",
                table: "VehicleModel");

            migrationBuilder.AddColumn<string>(
                name: "Make",
                table: "VehicleModel",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
