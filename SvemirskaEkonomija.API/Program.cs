using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using SvemirskaEkonomija.API.Rute;
using SvemirskaEkonomija.API.Servisi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
// 1. Izvlačenje konfiguracije za MongoDB (Ovo već imaš)
var mongoSection = builder.Configuration.GetSection("MongoSettings");
var connectionString = mongoSection["ConnectionString"] ?? "mongodb://localhost:27017";
var databaseName = mongoSection["EkonomijaDb"] ?? "Kosmos_Ekonomija"; 

builder.Services.AddSingleton<IMongoClient>(new MongoClient(connectionString));
builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
});

builder.Services.AddScoped<EkonomijaServis>();

// 2. Inicijalizacija JWT ključa (Mora biti ISTI ključ kao u Identitet API-ju!)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? "PodrazumevaniUltraTajniKljucZaLokalniRazvoj123!");

// 3. DODAVANJE AUTENTIFIKACIJE (Ovaj servis samo verifikuje tokene koje je Identitet izdao)
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

// 4. DODAVANJE AUTORIZACIJE
builder.Services.AddAuthorization();

// 5. CORS Konfiguracija
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

// 6. Middleware cevovod (Redosled je ključan!)
app.UseCors("DozvoliReactPolicy");

app.UseAuthentication(); // 1. Dešifruj token i proveri ko šalje zahtev
app.UseAuthorization();  // 2. Proveri da li ima pravo pristupa

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();

app.MapirajEkonomiju();
app.Run();