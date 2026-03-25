using Microsoft.EntityFrameworkCore.Migrations;

namespace SignupAPI.Migrations
{
    public partial class Vehiclemodelupdate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Make",
                table: "VehicleModel",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Make",
                table: "VehicleModel");
        }
    }
}
