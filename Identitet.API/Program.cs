using System.Text;
using AspNetCore.Identity.MongoDbCore.Extensions;
using AspNetCore.Identity.MongoDbCore.Infrastructure; // <-- Potrebno za MongoDbIdentityConfiguration
using Identitet.API.Modeli;
using Identitet.API.Rute;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

if (!BsonClassMap.IsClassMapRegistered(typeof(Korisnik.ApplicationUser)))
{
    BsonClassMap.RegisterClassMap<Korisnik.ApplicationUser>(cm =>
    {
        cm.AutoMap(); // Tells the driver to map standard base fields automatically
        cm.MapProperty(c => c.Ime).SetElementName("Ime");
        cm.MapProperty(c => c.Prezime).SetElementName("Prezime");
        cm.MapProperty(c => c.NazivFlote).SetElementName("NazivFlote");
    });
}

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();

// 1. Izvlačenje konfiguracije za MongoDB
var mongoSection = builder.Configuration.GetSection("MongoSettings");
var connectionString = mongoSection["ConnectionString"] ?? "mongodb://localhost:27017";
var databaseName = mongoSection["IdentitetDb"] ?? "Kosmos_Identitet"; // Ispravljeno sa IdentititetDb na IdentitetDb

// 2. Kreiranje i definisanje mongoDbIdentityConfig objekta koji je nedostajao
var mongoDbIdentityConfig = new MongoDbIdentityConfiguration
{
    MongoDbSettings = new MongoDbSettings
    {
        ConnectionString = connectionString,
        DatabaseName = databaseName
    },
    IdentityOptionsAction = options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.User.RequireUniqueEmail = true;
    }
};

// 3. Konfiguracija MongoDB Identity-ja sa tvojim odvojenim klasama
builder.Services.ConfigureMongoDbIdentity<Korisnik.ApplicationUser, Uloge.ApplicationRole, Guid>(mongoDbIdentityConfig)
    .AddDefaultTokenProviders();

// 4. Inicijalizacija JWT ključa
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret ključ nije pronađen!"));

// 5. DODAVANJE AUTENTIFIKACIJE (Sa JWT Bearer šemom)
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "IdentitetAPI",
            ValidAudience = jwtSettings["Audience"] ?? "KosmickaFlota",
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };
    });

builder.Services.AddAuthorization();
// 6. Standardne MongoDB registracije (za tvoje custom repozitorijume ako zatrebaju)
builder.Services.AddSingleton<IMongoClient>(new MongoClient(connectionString));
builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
});

// 7. CORS i Autorizacija
builder.Services.AddCors(options =>
{
    options.AddPolicy("DozvoliReactPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173") 
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

// Middleware cevovod (Redosled je ispravan!)
app.UseCors("DozvoliReactPolicy");
app.UseAuthentication(); 
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();
app.MapirajAutentifikaciju();
app.Run();