using System.Security.Claims;
using Identitet.API.OPP;
using Microsoft.AspNetCore.Mvc;
using SvemirskaEkonomija.API.Servisi;

namespace SvemirskaEkonomija.API.Rute;

public static class EkonomijaRute
{
    public static void MapirajEkonomiju(this WebApplication app)
    {
        var ekonomijaGrupa = app.MapGroup("/api/ekonomija");

        ekonomijaGrupa.MapPost("/pocetno-stanje", async ([FromBody] PocetniBrodOpp opp, EkonomijaServis servis) =>
        {
            await servis.PocetniBrodAsync(opp.KorisnikId, opp.NazivFlote);
            return Results.Ok(new { Poruka = "Pocetni brod i krediti uspesno dodati!" });
        });

        ekonomijaGrupa.MapPost("/kupi-brod", async ([FromBody] KupiBrodOpp opp, EkonomijaServis servis) =>
        {
            // Provera da li su prosleđeni validni podaci
            if (string.IsNullOrEmpty(opp.KorisnikId) || string.IsNullOrEmpty(opp.TipBroda))
            {
                return Results.BadRequest("KorisnikId i TipBroda su obavezna polja.");
            }

            var rezultat = await servis.KupiBrodAsync(opp.KorisnikId, opp.TipBroda);

            if (!rezultat.Uspeh)
            {
                return Results.BadRequest(rezultat.Poruka);
            }

            return Results.Ok(new { Poruka = rezultat.Poruka });
        });
        
        ekonomijaGrupa.MapDelete("/brodovi/{brodId}", async (string brodId, EkonomijaServis servis, ClaimsPrincipal korisnik) =>
        {
            var korisnikId = korisnik.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(korisnikId)) return Results.Unauthorized();

            var rezultat = await servis.ObrisiBrodAsync(korisnikId, brodId);
    
            return rezultat.Uspeh ? Results.Ok(rezultat.Poruka) : Results.BadRequest(rezultat.Poruka);
        }).RequireAuthorization();
        
        ekonomijaGrupa.MapPost("/prodaj-rudu", async ([FromBody] ProdajRuduOpp opp, EkonomijaServis servis) =>
        {
            var rezultat = await servis.ProdajRuduAsync(opp.KorisnikId, opp.TipRude, opp.Kolicina);
            if (!rezultat.Uspeh) return Results.NotFound("Korisnik nije pronađen u ekonomiji.");
    
            return Results.Ok(new { 
                Poruka = $"Sirovina uspešno procesirana.", 
                Zaradjeno = rezultat.Zaradjeno, 
                NoviSaldo = rezultat.NoviSaldo 
            });
        });
        
        ekonomijaGrupa.MapGet("/profil/{korisnikId}", async (string korisnikId, EkonomijaServis service) =>
        {
            var profil = await service.ProfilAsync(korisnikId);
            return profil is not null ? Results.Ok(profil) : Results.NotFound();
        }).RequireAuthorization();
        
        
    }
}