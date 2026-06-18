using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Univerzum.API.OPP;
using Univerzum.API.Servisi;

namespace Univerzum.API.Rute;

public static class UniverzumRute
{
    public static void MapirajUniverzum(this WebApplication app)
    {
        var univerzumGrupa = app.MapGroup("/api/univerzum");
        
        univerzumGrupa.MapPost("/generisi", async ([FromBody] GenerisiUniverzumOpp dto, UniverzumServis servis) =>
        {
            var rezultat = await servis.KreirajUniverzumAsync(dto.KorisnikId, dto.NazivSektora);
            if (!rezultat.Uspeh) return Results.BadRequest(rezultat.Poruka);
            return Results.Ok(rezultat.Univerzum);
        }).RequireAuthorization();

        univerzumGrupa.MapPost("/rudari", async ([FromBody] RudariAsteroidOpp opp, UniverzumServis servis, ClaimsPrincipal korisnik) =>
        {
            var korisnikId = korisnik.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    
            if (string.IsNullOrEmpty(korisnikId))
            {
                return Results.Unauthorized();
            }

            var rezultat = await servis.RudariAsteroidAsync(opp, korisnikId);
    
            return rezultat.Uspeh ? Results.Ok(rezultat.Poruka) : Results.BadRequest(rezultat.Poruka);
        }).RequireAuthorization();

        univerzumGrupa.MapGet("/moji-univerzumi/{korisnikId}", async (string korisnikId, UniverzumServis servis) =>
        {
            var univerzumi = await servis.NadjiAktivniUniverzumiAsync(korisnikId);
            return Results.Ok(univerzumi);
        }).RequireAuthorization();
        
        // 1. Ruta za detaljno listanje
        univerzumGrupa.MapGet("/listaj-sve", async (UniverzumServis servis) =>
        {
            var rezultati = await servis.IzlistajSveUniverzumeSaAsteroidimaAsync();
            return Results.Ok(rezultati);
        }).RequireAuthorization();

// 2. Ruta za brisanje univerzuma
        univerzumGrupa.MapDelete("/{univerzumId}", async (string univerzumId, UniverzumServis servis, ClaimsPrincipal korisnik) =>
        {
            var korisnikId = korisnik.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(korisnikId)) return Results.Unauthorized();

            var rezultat = await servis.ObrisiUniverzumAsync(univerzumId, korisnikId);

            return rezultat.Uspeh ? Results.Ok(rezultat.Poruka) : Results.BadRequest(rezultat.Poruka);
        }).RequireAuthorization();
    }
}