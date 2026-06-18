using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Univerzum.API.EkonomijaHttpKlijent;
using Univerzum.API.Rute;
using Univerzum.API.Servisi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

// 1. Izvlačenje konfiguracije za MongoDB 
var mongoSection = builder.Configuration.GetSection("MongoSettings");
var connectionString = mongoSection["ConnectionString"] ?? "mongodb://localhost:27017";
var databaseName = mongoSection["UniverzumDb"] ?? "Kosmos_Univerzum"; 

builder.Services.AddSingleton<IMongoClient>(new MongoClient(connectionString));
builder.Services.AddScoped(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(databaseName);
});

builder.Services.AddHttpClient<EkonomijaKlijent>(client =>
{
    client.BaseAddress = new Uri("http://svemirska-ekonomija-service:8080");
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<UniverzumServis>();

// 2. Inicijalizacija JWT ključa (Mora biti identičan ključ kao u Identitetu i Ekonomiji!)
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = Encoding.UTF8.GetBytes(jwtSettings["Secret"] ?? "PodrazumevaniUltraTajniKljucZaLokalniRazvoj123!");

// 3. DODAVANJE AUTENTIFIKACIJE (Samo verifikuje tokene)
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

// 6. Middleware cevovod (Redosled je zakon!)
app.UseCors("DozvoliReactPolicy");

app.UseAuthentication(); // Provera ko šalje zahtev
app.UseAuthorization();  // Provera prava pristupa

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// app.UseHttpsRedirection();
app.MapirajUniverzum();
app.Run();