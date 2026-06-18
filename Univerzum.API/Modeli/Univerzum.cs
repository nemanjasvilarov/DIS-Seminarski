using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Univerzum.API.Modeli;

public class Univerzum
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("KorisnikId")]
    public string KorisnikId { get; set; } = null!;

    [BsonElement("NazivSektora")]
    public string NazivSektora { get; set; } = null!;

    [BsonElement("JeIzrudaren")]
    public bool JeIzrudaren { get; set; } = false;

    [BsonElement("Pocetno")]
    public int Pocetno { get; set; }
}