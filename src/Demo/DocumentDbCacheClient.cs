using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Caching.Memory;

namespace Demo
{
    public class DocumentDbCacheClient
    {
        private readonly DocumentClient _client;
        private readonly IMemoryCache _cache;

        public DocumentDbCacheClient(DocumentClient client, IMemoryCache cache)
        {
            _client = client;
            _cache = cache;
        }

        public async Task<Document> GetDocumentById(string collectionLink, string id)
        {
            Document cacheEntry;
            var cacheKey = $"{collectionLink}:{id}";
            if (_cache.TryGetValue(cacheKey, out cacheEntry))
            {
                var ac = new AccessCondition { Condition = cacheEntry.ETag, Type = AccessConditionType.IfNoneMatch };
                var response = await _client.ReadDocumentAsync(cacheEntry.SelfLink, new RequestOptions { AccessCondition = ac });
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    return cacheEntry;
                }

                cacheEntry = response.Resource;
            }
            else
            {
                cacheEntry = (from f in _client.CreateDocumentQuery(collectionLink, new FeedOptions { EnableCrossPartitionQuery = true })
                              where f.Id == id
                              select f).AsEnumerable().FirstOrDefault();
            }

            _cache.Set(cacheKey, cacheEntry);
            return cacheEntry;
        }
    }
}
