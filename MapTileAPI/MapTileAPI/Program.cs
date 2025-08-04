using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Swagger/OpenAPI servisi ekle
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "MapTileAPI", Version = "v1" });
});

var jwtKey = "super_secret_jwt_key_123_456_789_ABC_DEF!";
var issuer = "MapTileAPI";
var audience = "MapTileManager";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "maptile.db");
var connStr = $"Data Source={dbPath}";

using (var conn = new SqliteConnection(connStr))
{
    conn.Open();
    var cmd = conn.CreateCommand();

    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS USERS (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        username TEXT NOT NULL UNIQUE,
        password TEXT NOT NULL
    );";
    cmd.ExecuteNonQuery();

    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS DATA (
        id INTEGER PRIMARY KEY AUTOINCREMENT,
        map_name TEXT,
        date TEXT,
        min_zoom INTEGER,
        max_zoom INTEGER,
        coord1_lat REAL,
        coord1_lng REAL,
        coord2_lat REAL,
        coord2_lng REAL,
        folder_path TEXT
    );";
    cmd.ExecuteNonQuery();

    cmd.CommandText = "SELECT COUNT(*) FROM USERS WHERE username = 'PAVOTEK'";
    var count = Convert.ToInt32(cmd.ExecuteScalar());
    if (count == 0)
    {
        string password = "STAJ2025@PAVOTEK.COM";
        string hash = HashPassword(password);
        cmd.CommandText = "INSERT INTO USERS (username, password) VALUES (@u, @p)";
        cmd.Parameters.AddWithValue("@u", "PAVOTEK");
        cmd.Parameters.AddWithValue("@p", hash);
        cmd.ExecuteNonQuery();
        cmd.Parameters.Clear();
    }
    conn.Close();
}

var app = builder.Build();

// Swagger middleware'i ekle
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MapTileAPI v1");
});
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/login", (LoginRequest login) =>
{
    using var conn = new SqliteConnection(connStr);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT password FROM USERS WHERE username = @u";
    cmd.Parameters.AddWithValue("@u", login.Username);
    var dbPassword = cmd.ExecuteScalar() as string;
    if (dbPassword == null)
        return Results.Unauthorized();

    string hash = HashPassword(login.Password);
    if (dbPassword != hash)
        return Results.Unauthorized();

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, login.Username)
    };
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: creds
    );
    var jwt = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Json(new { token = jwt });
});

app.MapPost("/register", (LoginRequest register) =>
{
    using var conn = new SqliteConnection(connStr);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM USERS WHERE username = @u";
    cmd.Parameters.AddWithValue("@u", register.Username);
    var count = Convert.ToInt32(cmd.ExecuteScalar());
    if (count > 0)
        return Results.BadRequest(new { error = "Kullanıcı adı zaten mevcut." });

    string hash = HashPassword(register.Password);
    cmd.CommandText = "INSERT INTO USERS (username, password) VALUES (@u, @p)";
    cmd.Parameters.AddWithValue("@p", hash);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Kayıt başarılı." });
});

app.MapGet("/", () => "Hello World!");

app.MapGet("/api/maps", (HttpContext http) =>
{
    using var conn = new SqliteConnection(connStr);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT id, map_name, date, min_zoom, max_zoom, coord1_lat, coord1_lng, coord2_lat, coord2_lng, folder_path FROM DATA";
    var reader = cmd.ExecuteReader();
    var maps = new List<object>();
    while (reader.Read())
    {
        maps.Add(new
        {
            id = reader.GetInt32(0),
            map_name = reader.GetString(1),
            date = reader.GetString(2),
            min_zoom = reader.GetInt32(3),
            max_zoom = reader.GetInt32(4),
            coord1_lat = reader.GetDouble(5),
            coord1_lng = reader.GetDouble(6),
            coord2_lat = reader.GetDouble(7),
            coord2_lng = reader.GetDouble(8),
            folder_path = reader.GetString(9)
        });
    }
    return Results.Json(maps);
}).RequireAuthorization();

app.MapPost("/api/maps", (MapData map) =>
{
    using var conn = new SqliteConnection(connStr);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = @"INSERT INTO DATA (map_name, date, min_zoom, max_zoom, coord1_lat, coord1_lng, coord2_lat, coord2_lng, folder_path) VALUES (@name, @date, @minz, @maxz, @c1lat, @c1lng, @c2lat, @c2lng, @folder)";
    cmd.Parameters.AddWithValue("@name", map.map_name);
    cmd.Parameters.AddWithValue("@date", map.date);
    cmd.Parameters.AddWithValue("@minz", map.min_zoom);
    cmd.Parameters.AddWithValue("@maxz", map.max_zoom);
    cmd.Parameters.AddWithValue("@c1lat", map.coord1_lat);
    cmd.Parameters.AddWithValue("@c1lng", map.coord1_lng);
    cmd.Parameters.AddWithValue("@c2lat", map.coord2_lat);
    cmd.Parameters.AddWithValue("@c2lng", map.coord2_lng);
    cmd.Parameters.AddWithValue("@folder", map.folder_path);
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Harita kaydedildi." });
}).RequireAuthorization();

app.MapPut("/api/maps/{id}", (int id, MapData map) =>
{
    using var conn = new SqliteConnection(connStr);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = @"UPDATE DATA SET map_name=@name, date=@date, min_zoom=@minz, max_zoom=@maxz, coord1_lat=@c1lat, coord1_lng=@c1lng, coord2_lat=@c2lat, coord2_lng=@c2lng, folder_path=@folder WHERE id=@id";
    cmd.Parameters.AddWithValue("@name", map.map_name);
    cmd.Parameters.AddWithValue("@date", map.date);
    cmd.Parameters.AddWithValue("@minz", map.min_zoom);
    cmd.Parameters.AddWithValue("@maxz", map.max_zoom);
    cmd.Parameters.AddWithValue("@c1lat", map.coord1_lat);
    cmd.Parameters.AddWithValue("@c1lng", map.coord1_lng);
    cmd.Parameters.AddWithValue("@c2lat", map.coord2_lat);
    cmd.Parameters.AddWithValue("@c2lng", map.coord2_lng);
    cmd.Parameters.AddWithValue("@folder", map.folder_path);
    cmd.Parameters.AddWithValue("@id", id);
    int affected = cmd.ExecuteNonQuery();
    if (affected == 0) return Results.NotFound();
    return Results.Ok(new { message = "Harita güncellendi." });
}).RequireAuthorization();

app.MapDelete("/api/maps/{id}", (int id) =>
{
    using var conn = new SqliteConnection(connStr);
    conn.Open();
    var cmd = conn.CreateCommand();
    cmd.CommandText = "SELECT folder_path FROM DATA WHERE id=@id";
    cmd.Parameters.AddWithValue("@id", id);
    var folderPath = cmd.ExecuteScalar() as string;
    if (folderPath == null) return Results.NotFound();
    // Klasörü sil
    if (Directory.Exists(folderPath))
    {
        Directory.Delete(folderPath, true);
    }
    // Veritabanından sil
    cmd.CommandText = "DELETE FROM DATA WHERE id=@id";
    cmd.ExecuteNonQuery();
    return Results.Ok(new { message = "Harita ve klasörü silindi." });
}).RequireAuthorization();

app.Run();

// ===== FONKSİYONLAR VE RECORD'LAR AYRI ALANDA OLMALI =====

static string HashPassword(string input)
{
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLower();
}

public record LoginRequest(string Username, string Password);

public record MapData(
    string map_name,
    string date,
    int min_zoom,
    int max_zoom,
    double coord1_lat,
    double coord1_lng,
    double coord2_lat,
    double coord2_lng,
    string folder_path
);
