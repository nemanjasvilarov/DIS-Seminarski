namespace Univerzum.API.EkonomijaHttpKlijent;

public class EkonomijaKlijent
{
    private readonly HttpClient _httpClient;

    // .NET će automatski ubrizgati HttpClient konfigurisan za Ekonomiju
    public EkonomijaKlijent(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> ProdajRuduAsync(string korisnikId, string tipRude, double kolicina)
    {
        try
        {
            var isplataPodaci = new { KorisnikId = korisnikId, TipRude = tipRude, Kolicina = kolicina };
            
            var odgovor = await _httpClient.PostAsJsonAsync("api/ekonomija/prodaj-rudu", isplataPodaci);
            
            return odgovor.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[EkonomijaClient] Mrežna greška: {ex.Message}");
            return false;
        }
    }
}