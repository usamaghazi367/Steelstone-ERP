using Microsoft.Data.SqlClient;
var conn = "Data Source=sql5112.site4now.net;Initial Catalog=db_acd70a_ghaziusama;User Id=db_acd70a_ghaziusama_admin;Password=Usama@123;Encrypt=True;TrustServerCertificate=True;";
await using var db = new SqlConnection(conn);
await db.OpenAsync();
var check = new SqlCommand("SELECT COUNT(1) FROM Users WHERE Email=@e", db);
check.Parameters.AddWithValue("@e", "admin@constfire.com");
var count = (int)(await check.ExecuteScalarAsync() ?? 0);
if (count == 0) {
  var hash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
  var ins = new SqlCommand("INSERT INTO Users (Email,PasswordHash,Name,Role,CreatedAt) VALUES (@e,@p,@n,@r,GETUTCDATE())", db);
  ins.Parameters.AddWithValue("@e", "admin@constfire.com");
  ins.Parameters.AddWithValue("@p", hash);
  ins.Parameters.AddWithValue("@n", "Admin User");
  ins.Parameters.AddWithValue("@r", "Admin");
  await ins.ExecuteNonQueryAsync();
  Console.WriteLine("Admin user inserted");
} else { Console.WriteLine("Admin already exists"); }
