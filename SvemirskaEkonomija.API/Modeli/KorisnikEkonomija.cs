using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Identitet.API.Modeli;

public class KorisnikEkonomija
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("KorisnikId")]
    public string KorisnikId { get; set; } = null!;

    [BsonElement("Krediti")]
    public long Krediti { get; set; }
}