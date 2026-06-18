using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Identitet.API.Modeli;

public class SvemirskiBrod
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("KorisnikId")]
    public string KorisnikId { get; set; } = null!;

    [BsonElement("Naziv")]
    public string Naziv { get; set; } = null!;

    [BsonElement("KapacitetSkladista")]
    public double KapacitetSkladista { get; set; }

    [BsonElement("BrzinaRudarenja")]
    public double BrzinaRudarenja { get; set; }
}