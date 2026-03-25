using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SignupAPI.Migrations
{
    public partial class GarageVechicleOwner : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "GarageOwned",
                table: "CustomerVehicle",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "GarageVehicleOwner",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GarageVehicleOwnerName = table.Column<string>(nullable: true),
                    GarageBusinessId = table.Column<int>(nullable: false),
                    CreatedDate = table.Column<DateTime>(nullable: false),
                    CreatedBy = table.Column<string>(nullable: true),
                    UpdatedDate = table.Column<DateTime>(nullable: true),
                    UpdatedBy = table.Column<string>(nullable: true),
                    Active = table.Column<bool>(nullable: false),
                    Blocked = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GarageVehicleOwner", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GarageVehicleOwner");

            migrationBuilder.DropColumn(
                name: "GarageOwned",
                table: "CustomerVehicle");
        }
    }
}
