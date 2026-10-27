using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SignupAPI.Models;

static class SeedFile
{
 public static void Write(SignupContext db, string path)
 {
  var now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
  var rows = new List<object> {
   new VehicleMake {Id=1, Make="Synthetic Motors", Active=true, CreatedDate=now},
   new VehicleModel {Id=1, MakeId=1, Model="Demo Hatchback", Active=true, CreatedDate=now},
   new VehicleYears {Id=1, Years="2020", Active=true, CreatedDate=now},
   new FuelType {Id=1, Fuel="Petrol", Active=true, CreatedDate=now},
   new TransmissionType {Id=1, Transmission="Manual", Active=true, CreatedDate=now},
   new Mileage {Id=1, Kilometers="50000", Active=true, CreatedDate=now}
  };
  for (var g=1; g<=2; g++) rows.Add(new GarageBusiness {Id=g, GarageBusinessName=$"SYNTHETIC Staging Garage {g}", GarageAddressLine1="Synthetic Test Street", GarageAddressLine2="Dublin", GarageEmailAddress=$"garage{g}@example.invalid", Active=true, CreatedDate=now, CreatedBy="staging-fixture"});
  rows.Add(new Users {Id=1, FirstName="Staging", LastName="Tester", EmailAddress="tester@example.invalid", Active=true, GarageBusinessId=1, CreatedDate=now, CreatedBy="staging-fixture"});
  for (var g=1; g<=2; g++) {
   rows.Add(new GarageBusinessUser {Id=g, GarageBusinessId=g, UserId=1, Role="Owner", IsOwner=true, IsActive=true, CreatedDate=now, CreatedBy="staging-fixture"});
   rows.Add(new GarageBusinessCustomer {Id=g, GarageBusinessId=g, GarageCustomerForename="Synthetic", GarageCustomerSurname=$"Customer {g}", GarageCustomerEmailAddress=$"customer{g}@example.invalid", Active=true, CreatedDate=now});
   rows.Add(new CustomerVehicle {Id=g, GarageBusinessId=g, VehicleMakeId=1, VehicleModelId=1, VehicleYearId=1, VehicleFuelTypeId=1, VehicleTransmissionId=1, VehicleMileageId=1, VehicleMileageDate=now, VehicleRegistration=$"STAGING-{g}", Active=true, GarageOwned=false, CreatedDate=now});
   rows.Add(new CustomerOwnedVehicles {Id=g, GarageBusinessId=g, GarageBusinessCustomerId=g, VehicleId=g, Active=true, CreatedDate=now});
   rows.Add(new WorkItem {Id=g, GarageBusinessCustomerId=g, CustomerId=g, VehicleId=g, RepairInstructions=$"SYNTHETIC garage {g}: replace brake pads and inspect discs", CreatedDate=now});
   // Financial records are deterministic fixtures using the existing 23.5% calculation and numbering.
   // An additional unconverted quote per garage supports the actual conversion acceptance test.
   foreach(var q in new[]{g, g+2}) {
    rows.Add(new WorkQuote {Id=q, GarageBusinessCustomerId=g, CustomerId=g, VehicleId=g, Labour=100m, SubTotal=100m, Vat=23.50m, Total=123.50m, Tax=23.5m, WorkQuoteDate=now, QuoteDate=now, CreatedDate=now, Comment="SYNTHETIC TEST DOCUMENT"});
    rows.Add(new WorkQuoteWorkItem {Id=q, GarageBusinessCustomerId=g, WorkQuoteId=q, WorkItemId=g, CreatedDate=now});
   }
   rows.Add(new VehicleInvoice {Id=g, GarageBusinessId=g, CustomerId=g, GarageBusinessCustomerId=g, VehicleId=g, WorkQuoteId=g, InvoiceNumber=$"INV-2026-{g:D6}", InvoiceDate=now, DateDue=now.AddDays(14), Labour=100m, SubTotal=100m, Vat=23.50m, Total=123.50m, Tax="23.5", InvoiceStatus=g==1?"Paid":"Pending", Paid=g==1, PaidDate=g==1?now:null, DatePaid=g==1?now:null, InvoiceDescription="SYNTHETIC TEST DOCUMENT", CreatedDate=now});
   rows.Add(new InvoiceWorkQuote {Id=g, GarageBusinessCustomerId=g, InvoiceId=g, WorkQuoteId=g, CreatedDate=now});
  }
  var sql = new StringBuilder("SET NOCOUNT ON;\nSET XACT_ABORT ON;\nIF DB_NAME() <> N'GarageSaas_Staging' THROW 51000, 'Wrong database.', 1;\nIF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) NOT IN (N'garagesaasdbserver',N'garagesaasdbserver.database.windows.net') THROW 51000, 'Wrong server.', 1;\nIF EXISTS(SELECT 1 FROM dbo.GarageBusiness) OR EXISTS(SELECT 1 FROM dbo.Users) OR EXISTS(SELECT 1 FROM dbo.VehicleMake) THROW 51000, 'Fixture requires fresh empty tables; never replay.', 1;\nBEGIN TRANSACTION;\n");
  foreach(var row in rows) {
   var entity=db.Model.FindEntityType(row.GetType())!;
   var table=StoreObjectIdentifier.Table(entity.GetTableName()!,entity.GetSchema());
   var properties=entity.GetProperties().Where(p=>!p.IsShadowProperty()).ToArray();
   var name="[dbo].["+entity.GetTableName()+"]";
   sql.AppendLine($"SET IDENTITY_INSERT {name} ON;");
   sql.AppendLine($"INSERT INTO {name} ({string.Join(",",properties.Select(p=>"["+p.GetColumnName(table)+"]"))}) VALUES ({string.Join(",",properties.Select(p=>Literal(p.PropertyInfo!.GetValue(row))))});");
   sql.AppendLine($"SET IDENTITY_INSERT {name} OFF;");
  }
  sql.AppendLine("COMMIT TRANSACTION;");
  File.WriteAllText(path,sql.ToString());
 }
 static string Literal(object? value) => value switch {
  null=>"NULL", bool b=>b?"1":"0", string s=>"N'"+s.Replace("'","''")+"'",
  DateTime d=>"'"+d.ToString("yyyy-MM-ddTHH:mm:ss.fff",CultureInfo.InvariantCulture)+"'",
  _=>Convert.ToString(value,CultureInfo.InvariantCulture)!
 };
}
