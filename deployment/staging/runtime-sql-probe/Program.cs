using Microsoft.Data.SqlClient;
using System.Text.Json;
// Ephemeral staging-worker verification only. No writes or secret/token output.
try {
 using var sql=new SqlConnection("Server=tcp:garagesaasdbserver.database.windows.net,1433;Database=GarageSaas_Staging;Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Persist Security Info=False;Connection Timeout=30;");
 await sql.OpenAsync();
 using var command=new SqlCommand("SELECT DB_NAME(),USER_NAME(),(SELECT COUNT(*) FROM dbo.GarageBusiness),(SELECT COUNT(*) FROM dbo.WorkQuote),(SELECT COUNT(*) FROM dbo.VehicleInvoice)",sql);
 using var r=await command.ExecuteReaderAsync();
 if(!await r.ReadAsync()||r.GetString(0)!="GarageSaas_Staging"||r.GetString(1)!="garagesaas-stg-d49b2f61-weu"||r.GetInt32(2)!=2||r.GetInt32(3)!=4||r.GetInt32(4)!=2)throw new InvalidOperationException();
 Console.WriteLine(JsonSerializer.Serialize(new{success=true,database=r.GetString(0),identity=r.GetString(1),garages=r.GetInt32(2),quotes=r.GetInt32(3),invoices=r.GetInt32(4),authentication="Active Directory Managed Identity",tlsCertificateValidation=true}));
}catch(SqlException e){Console.WriteLine(JsonSerializer.Serialize(new{success=false,error="SQL failure",number=e.Number}));Environment.ExitCode=1;}
catch(Exception e){Console.WriteLine(JsonSerializer.Serialize(new{success=false,error=e.GetType().Name}));Environment.ExitCode=1;}
