using MongoDB.Driver;
using Univerzum.API.EkonomijaHttpKlijent;
using Univerzum.API.Modeli;
using Univerzum.API.OPP;

namespace Univerzum.API.Servisi;

public class UniverzumServis
{
    private readonly IMongoCollection<Modeli.Univerzum> _univerzumi;
    private readonly IMongoCollection<Asteroid> _asteroidBuckets;
    private readonly EkonomijaKlijent _ekonomijaClient;
    
    public UniverzumServis(IMongoClient mongoClient, EkonomijaKlijent ekonomijaClient)
    {
        var database = mongoClient.GetDatabase("Kosmos_Univerzum");
        _univerzumi = database.GetCollection<Modeli.Univerzum>("Univerzumi");
        _asteroidBuckets = database.GetCollection<Asteroid>("Asteroid");
        _ekonomijaClient = ekonomijaClient;
    }

    public async Task<(bool Uspeh, string Poruka, Modeli.Univerzum? Univerzum)> KreirajUniverzumAsync(string korisnikId, string nazivSektora)
    {
        // Provera limita: Broj aktivnih (neizrudarenih) univerzuma
        var aktivniBroj = await _univerzumi.CountDocumentsAsync(u => u.KorisnikId == korisnikId && !u.JeIzrudaren);
        
        if (aktivniBroj >= 3)
        {
            return (false, "Dostigli ste limit od 3 aktivna univerzuma. Izrudarite barem jedan u potpunosti da biste otvorili novi!", null);
        }

        var rand = new Random();
        var noviUniverzum = new Modeli.Univerzum
        {
            KorisnikId = korisnikId,
            NazivSektora = nazivSektora,
            Pocetno = rand.Next(100000, 999999),
            JeIzrudaren = false
        };

        await _univerzumi.InsertOneAsync(noviUniverzum);

        // Proceduralno generisanje i pakovanje u Bucket (Sektor X0_Y0)
        var bucket = new Asteroid
        {
            UniverzumId = noviUniverzum.Id,
            SektorKvadrant = "X0_Y0",
            Asteroidi = Enumerable.Range(1, 5).Select(i => new AsteroidPodaci
            {
                Pozicija = $"X:{rand.Next(0, 100)},Y:{rand.Next(0, 100)}",
                TipRude = i % 2 == 0 ? "Gvozdje" : "Platinijum",
                PocetnaKolicina = 500.0,
                PreostalaKolicina = 500.0
            }).ToList()
        };

        await _asteroidBuckets.InsertOneAsync(bucket);

        return (true, "Univerzum uspesno generisan!", noviUniverzum);
    }

    public async Task<(bool Uspeh, string Poruka)> RudariAsteroidAsync(RudariAsteroidOpp opp, string korisnikId)
    {
       // 1. Pronalaženje bucketa i provera asteroida (Horizontalno + Vertikalno particionisano)
        var filter = Builders<Asteroid>.Filter.And(
            Builders<Asteroid>.Filter.Eq(b => b.UniverzumId, opp.UniverzumId),
            Builders<Asteroid>.Filter.Eq(b => b.SektorKvadrant, opp.SektorKvadrant),
            Builders<Asteroid>.Filter.ElemMatch(b => b.Asteroidi, a => a.AsteroidId == opp.AsteroidId)
        );

        var bucket = await _asteroidBuckets.Find(filter).FirstOrDefaultAsync();
        if (bucket == null) return (false, "Sektor ili asteroid nije pronađen.");

        var asteroid = bucket.Asteroidi.First(a => a.AsteroidId == opp.AsteroidId);
        if (asteroid.PreostalaKolicina <= 0) return (false, "Ovaj asteroid je već potpuno iscrpljen!");

        double stvarnaKolicinaZaRudarenje = Math.Min(asteroid.PreostalaKolicina, opp.KolicinaZaRudarenje);
        double novaKolicina = asteroid.PreostalaKolicina - stvarnaKolicinaZaRudarenje;

        // Ažuriranje unutar Mongo Bucketa
        var update = Builders<Asteroid>.Update.Set("Asteroidi.$.PreostalaKolicina", novaKolicina);
        await _asteroidBuckets.UpdateOneAsync(filter, update);

        // 2. Poziv ka Ekonomiji preko izolovanog klijenta
        bool isplataUspesna = await _ekonomijaClient.ProdajRuduAsync(korisnikId, asteroid.TipRude, stvarnaKolicinaZaRudarenje);
        if (!isplataUspesna)
        {
            // Loguješ, ali ne blokiraš rudarenje sirovine
            Console.WriteLine($"[UniverzumService] Isplata zakazala za korisnika {korisnikId}");
        }

        // 3. Provera da li je celi univerzuma očišćen
        var sviBucketi = await _asteroidBuckets.Find(b => b.UniverzumId == opp.UniverzumId).ToListAsync();
        bool sveIzrudareno = sviBucketi.SelectMany(b => b.Asteroidi).All(a => a.PreostalaKolicina <= 0);

        if (sveIzrudareno)
        {
            await _univerzumi.UpdateOneAsync(
                u => u.Id == opp.UniverzumId,
                Builders<Modeli.Univerzum>.Update.Set(u => u.JeIzrudaren, true)
            );
        }

        return (true, $"Uspešno izrudareno {stvarnaKolicinaZaRudarenje} jedinica {asteroid.TipRude}!");
    }
    
    public async Task<(bool Uspeh, string Poruka)> ObrisiUniverzumAsync(string univerzumId, string korisnikId)
    {
        // 1. Provera vlasništva nad univerzumom pre brisanja
        var univerzum = await _univerzumi.Find(u => u.Id.ToString() == univerzumId && u.KorisnikId == korisnikId).FirstOrDefaultAsync();
        if (univerzum == null) 
        {
            return (false, "Svemirski sektor nije pronađen ili nemate administratorsku dozvolu za njegovo uništenje.");
        }

        // 2. Kaskadno brisanje: Čistimo sve buckete asteroida vezane za taj svemir
        await _asteroidBuckets.DeleteManyAsync(b => b.UniverzumId == univerzumId);

        // 3. Brisanje glavnog dokumenta univerzuma
        var rezultat = await _univerzumi.DeleteOneAsync(u => u.Id.ToString() == univerzumId);

        if (rezultat.DeletedCount == 0)
        {
            return (false, "Greška prilikom pokretanja supernove nad sektorom.");
        }

        return (true, $"Univerzum u sektoru '{univerzum.NazivSektora}' i svi njegovi resursi su uspešno izbrisani iz postojanja.");
    }

    public async Task<List<DetaljiUniverzumOpp>> IzlistajSveUniverzumeSaAsteroidimaAsync()
    {
        var univerzumi = await _univerzumi.Find(_ => true).ToListAsync();
        var odgovor = new List<DetaljiUniverzumOpp>();

        foreach (var uni in univerzumi)
        {
            var bucketi = await _asteroidBuckets.Find(b => b.UniverzumId == uni.Id.ToString()).ToListAsync();
        
            // Mapiranje u AsteroidDetalji preko konstruktora record-a
            var sviAsteroidiIzBucketa = bucketi.SelectMany(b => b.Asteroidi)
                .Select(a => new AsteroidDetaljiOpp(
                    a.AsteroidId,
                    a.TipRude,
                    a.PocetnaKolicina,
                    a.PreostalaKolicina
                )).ToList();

            // Kreiranje glavnog record-a
            odgovor.Add(new DetaljiUniverzumOpp(
                uni.Id.ToString(),
                uni.NazivSektora,
                uni.JeIzrudaren,
                sviAsteroidiIzBucketa
            ));
        }

        return odgovor;
    }
    
    public async Task<List<Modeli.Univerzum>> NadjiAktivniUniverzumiAsync(string korisnikId) =>
        await _univerzumi.Find(u => u.KorisnikId == korisnikId).ToListAsync();
}