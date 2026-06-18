using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Univerzum.API.Modeli;

public class Asteroid
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("UniverzumId")]
    public string UniverzumId { get; set; } = null!;

    [BsonElement("SektorKvadrant")]
    public string SektorKvadrant { get; set; } = null!; 

    [BsonElement("Asteroidi")]
    public List<AsteroidPodaci> Asteroidi { get; set; } = new();
}