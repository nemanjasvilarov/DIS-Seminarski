using Identitet.API.Modeli;
using MongoDB.Driver;

namespace SvemirskaEkonomija.API.Servisi;

public class EkonomijaServis
{
    private readonly IMongoCollection<KorisnikEkonomija> _korisnici;
    private readonly IMongoCollection<SvemirskiBrod> _brodovi;

    public EkonomijaServis(IMongoClient mongoClient)
    {
        var database = mongoClient.GetDatabase("Kosmos_Ekonomija");
        _korisnici = database.GetCollection<KorisnikEkonomija>("Korisnici");
        _brodovi = database.GetCollection<SvemirskiBrod>("Brodovi");
    }

    public async Task PocetniBrodAsync(string korisnikId, string nazivFlote)
    {
        var postojiKorisnik = await _korisnici.Find(k => k.KorisnikId == korisnikId).AnyAsync();
        if (postojiKorisnik) return;
        
        await _korisnici.InsertOneAsync(new KorisnikEkonomija 
        { 
            KorisnikId = korisnikId, 
            Krediti = 1000 
        });
        
        await _brodovi.InsertOneAsync(new SvemirskiBrod
        {
            KorisnikId = korisnikId,
            Naziv = $"Ekspedicija {nazivFlote} I",
            KapacitetSkladista = 100.0,
            BrzinaRudarenja = 5.0
        });
    }

    public async Task<(bool Uspeh, string Poruka)> KupiBrodAsync(string korisnikId, string tipBroda)
    {
        // 1. Definišemo "salone" svemirskih brodova i njihove cene
        var (cena, kapacitet, brzina, zvanicniNaziv) = tipBroda.ToLower() switch
        {
            "transporter" => (Cena: 500L, Kapacitet: 500.0, Brzina: 2.0, Naziv: "Teški Transporter"),
            "presretač"   => (Cena: 800L, Kapacitet: 50.0,  Brzina: 15.0, Naziv: "Brzi Presretač"),
            "rudar_v2"    => (Cena: 1200L, Kapacitet: 300.0, Brzina: 10.0, Naziv: "Napredni Rudar V2"),
            _ => (Cena: -1L, Kapacitet: 0.0, Brzina: 0.0, Naziv: "")
        };

        if (cena == -1L)
        {
            return (false, "Izabrani tip broda ne postoji u katalogu.");
        }

        // 2. Pronalazimo korisnika (Koristimo KorisnikId koji je naš Shard Key)
        var korisnik = await _korisnici.Find(k => k.KorisnikId == korisnikId).FirstOrDefaultAsync();
        if (korisnik == null)
        {
            return (false, "Ekonomski profil korisnika nije pronađen.");
        }

        // 3. Provera budžeta
        if (korisnik.Krediti < cena)
        {
            return (false, $"Nemate dovoljno kredita. Potrebno: {cena}, Trenutno imate: {korisnik.Krediti}.");
        }

        // 4. Naplata (Smanjujemo kredite korisniku)
        var noviKrediti = korisnik.Krediti - cena;
        var updateFilter = Builders<KorisnikEkonomija>.Filter.Eq(k => k.KorisnikId, korisnikId);
        var updateResult = Builders<KorisnikEkonomija>.Update.Set(k => k.Krediti, noviKrediti);
        await _korisnici.UpdateOneAsync(updateFilter, updateResult);

        // 5. Isporuka (Dodajemo novi brod u kolekciju brodova)
        var noviBrod = new SvemirskiBrod
        {
            KorisnikId = korisnikId,
            Naziv = $"{zvanicniNaziv} #{new Random().Next(100, 999)}",
            KapacitetSkladista = kapacitet,
            BrzinaRudarenja = brzina
        };
        await _brodovi.InsertOneAsync(noviBrod);

        return (true, $"Uspešno ste kupili {zvanicniNaziv}! Preostalo kredita: {noviKrediti}.");
    }
    
    public async Task<(bool Uspeh, string Poruka)> ObrisiBrodAsync(string korisnikId, string brodId)
    {
        // 1. Provera broja brodova za ovog korisnika (Mora ostati bar jedan)
        var brojBrodova = await _brodovi.CountDocumentsAsync(b => b.KorisnikId == korisnikId);
    
        if (brojBrodova <= 1)
        {
            return (false, "Kritična greška: Ne možete obrisati brod. Vaša flota mora imati bar jedan operativan brod!");
        }

        // 2. Provera da li taj specifičan brod uopšte postoji i pripada tom korisniku
        var filter = Builders<SvemirskiBrod>.Filter.And(
            Builders<SvemirskiBrod>.Filter.Eq("Id", brodId), // ili b => b.Id ako je mapirano
            Builders<SvemirskiBrod>.Filter.Eq(b => b.KorisnikId, korisnikId)
        );

        var brodPostoji = await _brodovi.Find(filter).AnyAsync();
        if (!brodPostoji)
        {
            return (false, "Traženi brod ne postoji u vašem hangaru.");
        }

        // 3. Brisanje dokumenta iz kolekcije "Brodovi"
        var rezultat = await _brodovi.DeleteOneAsync(filter);

        if (rezultat.DeletedCount == 0)
        {
            return (false, "Uklanjanje broda iz hangara nije uspelo.");
        }

        return (true, "Brod je uspešno otpisan iz flote i recikliran u staro gvožđe.");
    }
    
    public async Task<object?> ProfilAsync(string korisnikId)
    {
        var korisnik = await _korisnici.Find(k => k.KorisnikId == korisnikId).FirstOrDefaultAsync();
        
        if (korisnik == null) return null;
        
        var korisnickiBrodovi = await _brodovi.Find(b => b.KorisnikId == korisnikId).ToListAsync();

        return new { Korisnik = korisnik, Brodovi = korisnickiBrodovi };
    }
    
    public async Task<(bool Uspeh, long Zaradjeno, long NoviSaldo)> ProdajRuduAsync(string korisnikId, string tipRude, double kolicina)
    {
        // Određujemo cenu po jedinici rude
        long cenaPoJedinici = tipRude.ToLower() switch
        {
            "gvozdje"    => 2L,   // Jeftina, česta ruda
            "platinijum" => 10L,  // Skupocena ruda
            _            => 1L
        };

        long zarada = (long)(kolicina * cenaPoJedinici);

        // Pronalazimo i ažuriramo kredite korisnika (koristeći Shard Key: KorisnikId)
        var filter = Builders<KorisnikEkonomija>.Filter.Eq(k => k.KorisnikId, korisnikId);
        var korisnik = await _korisnici.Find(filter).FirstOrDefaultAsync();
    
        if (korisnik == null) return (false, 0, 0);

        long noviSaldo = korisnik.Krediti + zarada;
        var update = Builders<KorisnikEkonomija>.Update.Set(k => k.Krediti, noviSaldo);
        await _korisnici.UpdateOneAsync(filter, update);

        return (true, zarada, noviSaldo);
    }
    
    
}