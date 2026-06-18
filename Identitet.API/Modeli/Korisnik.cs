using AspNetCore.Identity.MongoDbCore.Models;
using MongoDB.Bson.Serialization.Attributes;
using MongoDbGenericRepository.Attributes;

namespace Identitet.API.Modeli;

public class Korisnik
{
    [CollectionName("Korisnici")]
    public class ApplicationUser : MongoIdentityUser<Guid>
    {
        [BsonElement("Ime")]
        public string Ime { get; set; } = string.Empty;
        [BsonElement("Prezime")]
        public string Prezime { get; set; } = string.Empty;
        [BsonElement("NazivFlote")]
        public string NazivFlote { get; set; } = string.Empty;
    }
}