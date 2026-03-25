using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SignupAPI.Migrations
{
    public partial class GarageCustomer_CustomerVehicle : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GarageBusinessCustomer",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GarageCustomerForename = table.Column<string>(nullable: true),
                    GarageCustomerSurname = table.Column<string>(nullable: true),
                    GarageCustomerAddressline1 = table.Column<string>(nullable: true),
                    GarageCustomerAddressline2 = table.Column<string>(nullable: true),
                    GarageCustomerAddressline3 = table.Column<string>(nullable: true),
                    GarageCustomerAddressline4 = table.Column<string>(nullable: true),
                    GarageCustomerPostcode = table.Column<string>(nullable: true),
                    GarageCustomerEmailAddress = table.Column<string>(nullable: true),
                    GarageCustomerPhoneNumber = table.Column<string>(nullable: true),
                    GarageCustomerMobileNumber = table.Column<string>(nullable: true),
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
                    table.PrimaryKey("PK_GarageBusinessCustomer", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerVehicle",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleModel = table.Column<string>(nullable: true),
                    VehicleMake = table.Column<string>(nullable: true),
                    VehicleYear = table.Column<string>(nullable: true),
                    VehicleMileage = table.Column<string>(nullable: true),
                    VehicleMileageDate = table.Column<DateTime>(nullable: false),
                    VehicleRegistration = table.Column<string>(nullable: true),
                    VehicleTransmission = table.Column<string>(nullable: true),
                    VehicleFuelType = table.Column<string>(nullable: true),
                    VehicleTaxDue = table.Column<string>(nullable: true),
                    VehicleNCTDue = table.Column<string>(nullable: true),
                    CustomerId = table.Column<int>(nullable: false),
                    PreviousCustomerId = table.Column<int>(nullable: false),
                    CreatedDate = table.Column<DateTime>(nullable: false),
                    CreatedBy = table.Column<string>(nullable: true),
                    UpdatedDate = table.Column<DateTime>(nullable: true),
                    UpdatedBy = table.Column<string>(nullable: true),
                    Active = table.Column<bool>(nullable: false),
                    Blocked = table.Column<bool>(nullable: false),
                    GarageBusinessCustomerId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerVehicle", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerVehicle_GarageBusinessCustomer_GarageBusinessCustomerId",
                        column: x => x.GarageBusinessCustomerId,
                        principalTable: "GarageBusinessCustomer",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerVehicle_GarageBusinessCustomerId",
                table: "CustomerVehicle",
                column: "GarageBusinessCustomerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerVehicle");

            migrationBuilder.DropTable(
                name: "GarageBusinessCustomer");
        }
    }
}
