namespace Univerzum.API.OPP;

public record DetaljiUniverzumOpp(string UniverzumId,
    string Naziv,
    bool JeIzrudaren,
    List<AsteroidDetaljiOpp> Asteroidi);