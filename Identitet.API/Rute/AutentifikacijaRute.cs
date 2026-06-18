using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Identitet.API.Modeli;
using Identitet.API.OPP;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;
namespace Identitet.API.Rute;

public static class AutentifikacijaRute
{
    public static void MapirajAutentifikaciju(this WebApplication app)
    {
        var autorizacijaGrupa = app.MapGroup("/api/auth");

        // 1. REGISTRACIJA
        autorizacijaGrupa.MapPost("/registracija", async (
            RegistracijaOpp model, 
            UserManager<Korisnik.ApplicationUser> upravnikKorisnika,
            IHttpClientFactory httpClientFactory) =>
        {
            var postojeciKorisnik = await upravnikKorisnika.FindByEmailAsync(model.Email);
            if (postojeciKorisnik != null) 
                return Results.BadRequest("Korisnik sa ovim Email-om već postoji.");

            var noviKorisnik = new Korisnik.ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                Ime = model.Ime,
                Prezime = model.Prezime,
                NazivFlote = model.NazivFlote
            };

            var rezultat = await upravnikKorisnika.CreateAsync(noviKorisnik, model.Password);
            if (!rezultat.Succeeded)
                return Results.BadRequest(rezultat.Errors);
            try
            {
               
                var klijent = httpClientFactory.CreateClient();
                
                string ekonomijaUrl = "http://svemirska-ekonomija-service:8080/api/ekonomija/pocetno-stanje";
                
                var seedPodaci = new { 
                    KorisnikId = noviKorisnik.Id.ToString(), // Šaljemo ID iz baze identiteta
                    NazivFlote = noviKorisnik.NazivFlote ?? "Nepoznata Flota" 
                };
                
                var odgovor = await klijent.PostAsJsonAsync(ekonomijaUrl, seedPodaci);

                if (!odgovor.IsSuccessStatusCode)
                {
                    var greskaSadrzaj = await odgovor.Content.ReadAsStringAsync();
                    await upravnikKorisnika.DeleteAsync(noviKorisnik);
                    return Results.BadRequest($"Registracija nije uspela zbog greške u ekonomskom servisu: {greskaSadrzaj}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Identitet.API] Kritična greška: Ekonomija nedostupna ({ex.Message}). Pokrećem brisanje korisnika...");
    
                // Brišemo korisnika koji je maločas napravljen da ne bi ostao "slep" u sistemu
                await upravnikKorisnika.DeleteAsync(noviKorisnik);
    
                return Results.StatusCode(503); 
            }
            
            return Results.Ok("Registracija uspešna! Vaš početni brod vas čeka u hangaru.");
        });

        // 2. LOGOVANJE (Prijava na sistem)
        autorizacijaGrupa.MapPost("/login", async (
            PrijavaOpp model, 
            UserManager<Korisnik.ApplicationUser> upravnikKorisnika, 
            IConfiguration konfiguracija) =>
        {
            var korisnik = await upravnikKorisnika.FindByEmailAsync(model.Email);
            if (korisnik == null || !await upravnikKorisnika.CheckPasswordAsync(korisnik, model.Password))
            {
                return Results.Unauthorized();
            }

            // Generisanje JWT Tokena
            var jwtPodesavanja = konfiguracija.GetSection("JwtSettings");
            var tajniKljuc = Encoding.UTF8.GetBytes(jwtPodesavanja["Secret"] ?? "PodrazumevaniUltraTajniKljucZaLokalniRazvoj123!");
            
            var tvrdnje = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, korisnik.Id.ToString()),
                new Claim(ClaimTypes.Email, korisnik.Email!),
                new Claim("Ime", korisnik.Ime),
                new Claim("NazivFlote", korisnik.NazivFlote)
            };

            var opisnikTokena = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(tvrdnje),
                Expires = DateTime.UtcNow.AddDays(7), 
                Issuer = jwtPodesavanja["Issuer"] ?? "IdentitetAPI",
                Audience = jwtPodesavanja["Audience"] ?? "KosmickaFlota",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(tajniKljuc), SecurityAlgorithms.HmacSha256Signature)
            };

            var rukovalacTokena = new JwtSecurityTokenHandler();
            var kreiraniToken = rukovalacTokena.CreateToken(opisnikTokena);
            var tekstualniToken = rukovalacTokena.WriteToken(kreiraniToken);

            return Results.Ok(new { Token = tekstualniToken, Email = korisnik.Email, NazivFlote = korisnik.NazivFlote });
        });
        
        // 4. IZMENA PROFILA
        autorizacijaGrupa.MapPut("/profil", async (
            IzmenaProfilaOpp model, 
            ClaimsPrincipal identitetUlogovanog, 
            UserManager<Korisnik.ApplicationUser> upravnikKorisnika) =>
        {
            var korisnikId = identitetUlogovanog.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(korisnikId)) return Results.Unauthorized();

            var korisnik = await upravnikKorisnika.FindByIdAsync(korisnikId);
            if (korisnik == null) return Results.NotFound("Korisnik nije pronađen.");

            if (!string.IsNullOrWhiteSpace(model.Ime)) {
                korisnik.Ime = model.Ime;
            }
    
            if (!string.IsNullOrWhiteSpace(model.Prezime)) {
                korisnik.Prezime = model.Prezime;
            }

            if (!string.IsNullOrWhiteSpace(model.NazivFlote)) {
                korisnik.NazivFlote = model.NazivFlote;
            }

            var rezultat = await upravnikKorisnika.UpdateAsync(korisnik);
            if (!rezultat.Succeeded) return Results.BadRequest(rezultat.Errors);

            return Results.Ok(new { Poruka = "Profil uspešno ažuriran!", NazivFlote = korisnik.NazivFlote });
        }).RequireAuthorization(); 
        
        autorizacijaGrupa.MapGet("/profil", async (
            ClaimsPrincipal identitetUlogovanog, 
            UserManager<Korisnik.ApplicationUser> upravnikKorisnika
            )=>{
            var korisnikId = identitetUlogovanog.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(korisnikId)) return Results.Unauthorized();

            var korisnik = await upravnikKorisnika.FindByIdAsync(korisnikId);
            if (korisnik == null) return Results.NotFound("Korisnik nije pronađen.");
            return Results.Ok(new 
            {
                korisnik.Ime,
                korisnik.Prezime,
                korisnik.NazivFlote
            });
        }).RequireAuthorization();
    }
}