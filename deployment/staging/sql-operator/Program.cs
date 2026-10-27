using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SignupAPI.Models;

// Operator tool: exact fixed staging target; no secret/token output or saved token files.
var stage=args.SingleOrDefault()??"preflight";
var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../"));
try {
 var process=new ProcessStartInfo("C:/Program Files/Microsoft SDKs/Azure/CLI2/python.exe") {RedirectStandardOutput=true,RedirectStandardError=true,UseShellExecute=false};
 foreach(var a in new[]{"-IBm","azure.cli","account","get-access-token","--resource","https://database.windows.net/","--tenant","90589b9f-6a39-4285-a798-5d72f6937826","-o","json"})process.ArgumentList.Add(a);
 using var child=Process.Start(process)!;
 var output=await child.StandardOutput.ReadToEndAsync();var error=await child.StandardError.ReadToEndAsync();await child.WaitForExitAsync();
 if(child.ExitCode!=0)throw new InvalidOperationException("SQL Entra token unavailable; provider response suppressed.");
 using var token=JsonDocument.Parse(output);
 using var sql=new SqlConnection("Server=tcp:garagesaasdbserver.database.windows.net,1433;Database=GarageSaas_Staging;Encrypt=True;TrustServerCertificate=False;Persist Security Info=False;Connection Timeout=30;");
 sql.AccessToken=token.RootElement.GetProperty("accessToken").GetString();
 await sql.OpenAsync();
 async Task Run(string text) {using var command=new SqlCommand(text,sql){CommandTimeout=120};await command.ExecuteNonQueryAsync();}
 async Task<int> Scalar(string text) {using var command=new SqlCommand(text,sql);return Convert.ToInt32(await command.ExecuteScalarAsync());}
 await Run("IF DB_NAME() <> N'GarageSaas_Staging' THROW 51000,'Wrong database',1; IF CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) NOT IN (N'garagesaasdbserver',N'garagesaasdbserver.database.windows.net') THROW 51000,'Wrong server',1;");
 if(stage=="preflight") {
  var count=await Scalar("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0");
  Console.WriteLine($"Trusted TLS / approved human Entra connection: succeeded. Staging user tables: {count}.");
  using(var ipCommand=new SqlCommand("SELECT CONVERT(nvarchar(48),CONNECTIONPROPERTY('client_net_address'))",sql)){var ip=Convert.ToString(await ipCommand.ExecuteScalarAsync());Console.WriteLine($"Administrator SQL-session source IP: {ip}");File.WriteAllText(Path.Combine(root,"SQL-ADMIN-SOURCE-IP.json"),JsonSerializer.Serialize(new {ip,observedUtc=DateTime.UtcNow}));}
  if(count!=0)throw new InvalidOperationException("STOP: staging is not empty; no initialization allowed.");
 } else if(new[]{"01-schema.sql","02-synthetic-seed.sql","03-runtime-access.sql","04-verify.sql"}.Contains(stage)) {
  var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"INITIALIZATION-SCRIPT-HASHES.json")));
  var expected=manifest.RootElement.EnumerateArray().Single(e=>e.GetProperty("File").GetString()==stage).GetProperty("Hash").GetString();
  var bytes=File.ReadAllBytes(Path.Combine(root,stage));
  if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))!=expected)throw new InvalidOperationException("Reviewed script hash mismatch.");
  foreach(var batch in Regex.Split(System.Text.Encoding.UTF8.GetString(bytes),@"^\s*GO\s*$",RegexOptions.Multiline|RegexOptions.IgnoreCase))if(!string.IsNullOrWhiteSpace(batch))await Run(batch);
  Console.WriteLine($"Approved staging script completed: {stage}");
 } else if(stage=="verify-schema") {
  using var model=new SignupContext(new DbContextOptionsBuilder<SignupContext>().UseSqlServer("Server=offline.invalid;Database=Offline").Options);
  var expected=model.Model.GetRelationalModel().Tables.ToArray();
  if(await Scalar("SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0")!=expected.Length)throw new InvalidOperationException("Unexpected table count.");
  using var cmd=new SqlCommand("SELECT t.name,c.name,ty.name,c.max_length,c.precision,c.scale,c.is_nullable,c.is_identity FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id WHERE t.is_ms_shipped=0",sql);
  using var reader=await cmd.ExecuteReaderAsync();
  var columns=new Dictionary<(string,string),(string,bool,bool)>();
  while(await reader.ReadAsync()){
   var type=reader.GetString(2);var length=reader.GetInt16(3);
   if(type is "nvarchar" or "varchar" or "varbinary")type+="("+(length==-1?"max":(type=="nvarchar"?length/2:length).ToString())+")";
   else if(type is "decimal" or "numeric")type+=$"({reader.GetByte(4)},{reader.GetByte(5)})";
   columns[(reader.GetString(0),reader.GetString(1))]=(type,reader.GetBoolean(6),reader.GetBoolean(7));
  }
  await reader.CloseAsync();
  var total=0;
  foreach(var table in expected)foreach(var col in table.Columns){total++;if(!columns.TryGetValue((table.Name,col.Name),out var actual)||actual.Item1.Replace(" ","")!=col.StoreType.Replace(" ","")||actual.Item2!=col.IsNullable)throw new InvalidOperationException($"Unexpected mapped column: {table.Name}.{col.Name}");}
  if(total!=columns.Count)throw new InvalidOperationException("Unexpected extra columns.");
  if(await Scalar("SELECT COUNT(*) FROM sys.foreign_keys")!=expected.Sum(t=>t.ForeignKeyConstraints.Count()))throw new InvalidOperationException("Foreign key count differs.");
  if(await Scalar("SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1")!=0)throw new InvalidOperationException("Disabled/untrusted foreign key.");
  Console.WriteLine($"Schema matches current model: {expected.Length} tables, {total} columns, mapped types/nullability, FK count/trust.");
 } else if(stage=="verify-fixtures") {
  using var db=new SignupContext(new DbContextOptionsBuilder<SignupContext>().UseSqlServer(sql).Options);
  void Require(bool value,string check){if(!value)throw new InvalidOperationException("Verification stopped: "+check);}
  Require(await db.GarageBusiness.CountAsync()==2 && await db.GarageBusinessCustomer.CountAsync()==2 && await db.CustomerVehicle.CountAsync()==2 && await db.WorkItem.CountAsync()==2 && await db.WorkQuote.CountAsync()==4 && await db.VehicleInvoice.CountAsync()==2 && await db.GarageBusinessUser.CountAsync()==2 && await db.Users.CountAsync()==1,"fixture counts");
  Require(!await db.Users.AnyAsync(u=>u.IsPlatformAdministrator||u.ExternalUserId!=null),"unbound non-admin tester");
  Require(!await db.CustomerOwnedVehicles.AnyAsync(o=>!db.CustomerVehicle.Any(v=>v.Id==o.VehicleId&&v.GarageBusinessId==o.GarageBusinessId)||!db.GarageBusinessCustomer.Any(c=>c.Id==o.GarageBusinessCustomerId&&c.GarageBusinessId==o.GarageBusinessId)),"ownership graph");
  Require(await db.VehicleInvoice.CountAsync(i=>i.SubTotal==100m&&i.Vat==23.5m&&i.Total==123.5m)==2,"invoice totals");
  Require(await db.WorkQuote.CountAsync(i=>i.SubTotal==100m&&i.Vat==23.5m&&i.Total==123.5m)==4,"quote totals");
  Require(await db.VehicleInvoice.CountAsync(i=>i.Paid==true&&i.InvoiceStatus=="Paid"&&i.PaidDate!=null&&i.DatePaid!=null)==1 && await db.VehicleInvoice.CountAsync(i=>i.Paid==false&&i.InvoiceStatus=="Pending"&&i.PaidDate==null&&i.DatePaid==null)==1,"payment states");
  var quotes=new GarageSaas.Services.WorkQuoteService(db);
  var invoices=new GarageSaas.Services.VehicleInvoiceService(db);
  var work=new GarageSaas.Services.WorkItemService(db);
  var history=new GarageSaas.Services.VehicleServiceHistoryService(db,work,invoices);
  for(var g=1;g<=2;g++){
   var other=g==1?2:1;
   Require(quotes.GetWorkQuote(g,g).Success&&!quotes.GetWorkQuote(g,other).Success,"garage-scoped quotes");
   Require(invoices.GetVehicleInvoice(g,g).Success&&!invoices.GetVehicleInvoice(g,other).Success,"garage-scoped invoices");
   Require(work.GetWorkItemsForVehicle(g,g).Success&&!work.GetWorkItemsForVehicle(g,other).Success,"garage-scoped work");
   var h=history.Get(g,g);Require(h.Success&&h.Data.Entries.Count==1&&!history.Get(g,other).Success,"garage-scoped history");
  }
  using(var checks=new SqlCommand("DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS",sql))using(var results=await checks.ExecuteReaderAsync()) {do{Require(!await results.ReadAsync(),"foreign/check constraint violation");}while(await results.NextResultAsync());}
  Require(await Scalar("SELECT COUNT(*) FROM sys.database_principals WHERE name=N'garagesaas-stg-d49b2f61-weu' AND type='E' AND CONVERT(uniqueidentifier,sid)='b3c68adc-9f6f-4808-b049-f9947726fa38'")==1,"managed identity SID");
  await Run("EXECUTE AS USER=N'garagesaas-stg-d49b2f61-weu';");
  try{
   Require(await Scalar("SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL')")==0 && await Scalar("SELECT HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CREATE TABLE')")==0 && await Scalar("SELECT IS_MEMBER('db_owner')")==0,"no runtime DDL/owner");
   foreach(var table in new[]{"CustomerVehicle","GarageBusinessCustomer","WorkItem","WorkQuote","VehicleInvoice","InvoiceWorkQuote","VehiclePart"})foreach(var permission in new[]{"SELECT","INSERT","UPDATE","DELETE"})Require(await Scalar($"SELECT HAS_PERMS_BY_NAME('dbo.{table}','OBJECT','{permission}')")==1,"required object grant");
   foreach(var table in new[]{"Mileage","GarageVehicleOwner","VehicleMake"})Require(await Scalar($"SELECT HAS_PERMS_BY_NAME('dbo.{table}','OBJECT','INSERT')")==0 && await Scalar($"SELECT HAS_PERMS_BY_NAME('dbo.{table}','OBJECT','SELECT')")==1,"read-only lookup");
   Require(await Scalar("SELECT HAS_PERMS_BY_NAME('dbo.ServiceHistory','OBJECT','SELECT')")==0 && await Scalar("SELECT HAS_PERMS_BY_NAME('dbo.GarageOwnedVehicles','OBJECT','SELECT')")==0,"unused legacy tables denied");
  }finally{await Run("REVERT;");}
  Console.WriteLine("Verified: synthetic counts/ownership, 23.5% VAT totals, Paid/Pending flags/dates, two-garage service read isolation and service history, all constraints, managed-identity SID and runtime permissions (EXECUTE AS; no runtime token obtained)."); } else if(stage=="diagnose-user") {
  using var command=new SqlCommand("SELECT name,type_desc,authentication_type_desc,DATALENGTH(sid),CONVERT(nvarchar(36),CONVERT(uniqueidentifier,sid)) FROM sys.database_principals WHERE name=N'garagesaas-stg-d49b2f61-weu'",sql);
  using var r=await command.ExecuteReaderAsync();
  while(await r.ReadAsync())Console.WriteLine($"Runtime principal metadata: {r.GetString(0)}, {r.GetString(1)}, {r.GetString(2)}, SID bytes={r.GetInt32(3)}, GUID={r.GetString(4)}"); }else throw new InvalidOperationException("Unsupported operator stage.");
}catch(SqlException e){Console.Error.WriteLine($"SQL stage {stage} failed; SQL error number {e.Number}, class {e.Class}. Message/data suppressed.");Environment.ExitCode=1;}
catch(InvalidOperationException e) when(e.Message.StartsWith("Verification stopped:",StringComparison.Ordinal)){Console.Error.WriteLine(e.Message);Environment.ExitCode=1;}
catch(Exception e){Console.Error.WriteLine($"SQL stage {stage} stopped: {e.GetType().Name}. No further stage executed.");Environment.ExitCode=1;}






