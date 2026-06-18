namespace Univerzum.API.Modeli;

public class AsteroidPodaci
{
    public string AsteroidId { get; set; } = Guid.NewGuid().ToString();
    public string Pozicija { get; set; } = null!;
    public string TipRude { get; set; } = null!;
    public double PocetnaKolicina { get; set; }
    public double PreostalaKolicina { get; set; }
}