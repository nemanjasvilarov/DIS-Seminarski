using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

namespace Identitet.API.Modeli;

public class Uloge
{
    [CollectionName("Uloge")]
    public class ApplicationRole : MongoIdentityRole<Guid>
    {
        
    }
}