using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;
using Xunit;

namespace Demo
{
    public class CachingTests
    {
        private readonly DocumentClient _client;
        private const string EndpointUrl = "https://localhost:8081";
        private const string AuthorizationKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
        private const string DatabaseId = "CachingDemo";
        private const string CollectionId = "Customers";

        public CachingTests()
        {
            _client = new DocumentClient(new Uri(EndpointUrl), AuthorizationKey);
        }

        [Fact]
        public async Task Should_return_same_document_if_not_modified()
        {
            // Setup our Database and add a new Customer
            var dbSetup = new DatabaseSetup(_client);
            await dbSetup.Init(DatabaseId, CollectionId);

            var customerId = Guid.NewGuid().ToString();
            var addCustomer = new Customer(customerId, "Demo");
            await dbSetup.AddCustomer(addCustomer);

            // Fetch out the Document (Customer)
            var cacheClient = new DocumentDbCacheClient(_client, new MemoryCache(new MemoryCacheOptions()));
            var document1 = await cacheClient.GetDocumentById(dbSetup.Collection.SelfLink, customerId);
            var document2 = await cacheClient.GetDocumentById(dbSetup.Collection.SelfLink, customerId);

            document1.GetHashCode().ShouldBe(document2.GetHashCode());
            document1.ETag.ShouldBe(document2.ETag);
        }

        [Fact]
        public async Task Should_return_new_document_if_modified()
        {
            // Setup our Database and add a new Customer
            var dbSetup = new DatabaseSetup(_client);
            await dbSetup.Init(DatabaseId, CollectionId);

            var customerId = Guid.NewGuid().ToString();
            var addCustomer = new Customer(customerId, "Demo");
            await dbSetup.AddCustomer(addCustomer);

            // Fetch out the Document (Customer)
            var cacheClient = new DocumentDbCacheClient(_client, new MemoryCache(new MemoryCacheOptions()));
            var document1 = await cacheClient.GetDocumentById(dbSetup.Collection.SelfLink, customerId);

            // Using Access Conditions gives us the ability to use the ETag from our fetched document for optimistic concurrency.
            var ac = new AccessCondition { Condition = document1.ETag, Type = AccessConditionType.IfMatch };

            // Replace our document, which will succeed with the correct ETag 
            await dbSetup.Client.ReplaceDocumentAsync(document1.SelfLink, (Customer)(dynamic)document1,
                new RequestOptions { AccessCondition = ac });

            // Fetch out the Document (Customer) again
            var document2 = await cacheClient.GetDocumentById(dbSetup.Collection.SelfLink, customerId);

            // Should be new Document with new ETag
            document1.GetHashCode().ShouldNotBe(document2.GetHashCode());
            document1.ETag.ShouldNotBe(document2.ETag);
        }
    }
}
