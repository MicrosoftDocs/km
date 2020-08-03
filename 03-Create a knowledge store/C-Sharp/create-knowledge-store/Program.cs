using System;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Configuration;
using Index = Microsoft.Azure.Search.Models.Index;
using System.Threading.Tasks;

namespace margies.search

{
    class Program
    {
        // Constants
        private const string DataSourceName = "margies-docs-cs";
        private const string ContainerName = "margies";
        private const string IndexName = "margies-index-cs";
        private const string SkillsetName = "margies-skillset-cs";
        private const string IndexerName = "margies-indexer-cs";

        // Global config settings
        private static string searchServiceName;
        private static string adminApiKey;
        private static string cognitiveServicesKey;
        private static string blobConnectionString;

        public static async Task Main(string[] args)
        {
            // Get config settings from AppSettings
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            searchServiceName = configuration["SearchServiceName"];
            adminApiKey = configuration["SearchServiceAdminApiKey"];
            cognitiveServicesKey = configuration["CognitiveServicesApiKey"];
            blobConnectionString = configuration["AzureBlobConnectionString"];

            // Create search service client
            SearchServiceClient searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(adminApiKey));

            // Loop until the user presses "q"
            Boolean quit = false;
            while(quit == false){
                Console.WriteLine("What do you want to do?");
                Console.WriteLine("1: Create Data Source");
                Console.WriteLine("2: Create Skillset");
                Console.WriteLine("3: Create Index");
                Console.WriteLine("4: Create and run Indexer");
                Console.WriteLine("q: Quit");
                var key = Console.ReadKey();
                switch(key.KeyChar.ToString()){
                    case "1":
                        CreateOrUpdateDataSource(searchClient, blobConnectionString);
                        break;
                    case "2":
                        await CreateSkillset();
                        break;
                    case "3":
                        CreateIndex(searchClient);
                        break;
                    case "4":
                        Indexer indexer = CreateIndexer(searchClient);
                        CheckIndexerOverallStatus(searchClient);
                        break;
                    case "q":
                        quit = true;
                        break;
                    default:
                        Console.WriteLine("\nChoose an option.");
                        break;
                }
            }
        }

        private static void CreateOrUpdateDataSource(SearchServiceClient serviceClient, string connectionString)
        {
            Console.WriteLine("\nCreating or updating data source...\n");

            DataSource dataSource = DataSource.AzureBlobStorage(
                name: DataSourceName,
                storageConnectionString: connectionString,
                containerName: ContainerName,
                description: "Documents for search");

            // The data source does not need to be deleted if it was already created
            // since we are using the CreateOrUpdate method
            try
            {
                serviceClient.DataSources.CreateOrUpdate(dataSource);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create or update the data source\n Exception message: {0}\n", e.Message);
            }
        }

        private static async Task CreateSkillset()
        {
            Console.WriteLine("\nDefining skillset...");
            try
            {
                Console.WriteLine("\nPosting REST request...");

                // Get JSON request body for skillset
                string filepath = "skillset.json";
                using (StreamReader r = new StreamReader(filepath))
                {
                    var json = r.ReadToEnd();
                    var skillsetBody = JObject.Parse(json);

                    // Update cognitive services key
                    skillsetBody["cognitiveServices"]["key"] = cognitiveServicesKey;

                    // Update storage connection string
                    skillsetBody["knowledgeStore"]["storageConnectionString"] = blobConnectionString;

                    // submit an HTTP REST request to the search service
                    string searchURI = "https://" + searchServiceName + ".search.windows.net";
                    HttpClient client = new HttpClient();
                    client.BaseAddress = new Uri(searchURI);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Add("api-key", adminApiKey);
                    var url = "/skillsets/margies-skillset-cs?api-version=2020-06-30-Preview";
                    HttpContent data = new StringContent(skillsetBody.ToString(), Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PutAsync(url, data);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create the skillset\n Exception message: {0}\n", e.Message);
            }
        }

        private static void CreateIndex(SearchServiceClient searchClient)
        {
            Console.WriteLine("\nCreating index...\n");

            // Define the index with a name and fields based on the MargiesIndex class
            var index = new Index()
            {
                Name = IndexName,
                Fields = FieldBuilder.BuildForType<MargiesIndex>()
            };

            // Create the index
            try
            {
                bool exists = searchClient.Indexes.Exists(index.Name);

                // Delete and recreate the index if it already exists
                if (exists)
                {
                    searchClient.Indexes.Delete(index.Name);
                }

                searchClient.Indexes.Create(index);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create the index\n Exception message: {0}\n", e.Message);
            }
        }

        private static Indexer CreateIndexer(SearchServiceClient searchClient)
        {
            Console.WriteLine("\nCreating indexer...");

            // Specify indexer configuration (we want to extract content and metadata, and generate normalized images)
            IDictionary<string, object> config = new Dictionary<string, object>();
            config.Add(
                key: "dataToExtract",
                value: "contentAndMetadata");
            config.Add(
                key: "imageAction",
                value: "generateNormalizedImages");

            // Map source data values to index fields
            List<FieldMapping> fieldMappings = new List<FieldMapping>();
            fieldMappings.Add(new FieldMapping(
                sourceFieldName: "metadata_storage_path",
                targetFieldName: "id",
                mappingFunction: new FieldMappingFunction(
                    name: "base64Encode")));
            fieldMappings.Add(new FieldMapping(
                sourceFieldName: "metadata_storage_path",
                targetFieldName: "url"));
            fieldMappings.Add(new FieldMapping(
                sourceFieldName: "metadata_storage_name",
                targetFieldName: "file_name"));
            fieldMappings.Add(new FieldMapping(
                sourceFieldName: "metadata_author",
                targetFieldName: "author"));
            fieldMappings.Add(new FieldMapping(
                sourceFieldName: "metadata_storage_size",
                targetFieldName: "size"));
            fieldMappings.Add(new FieldMapping(
                sourceFieldName: "metadata_storage_last_modified",
                targetFieldName: "last_modified"));

            // Map output from skillset to index fields
            List<FieldMapping> outputMappings = new List<FieldMapping>();
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/normalized_images/*/imageDescription",
                targetFieldName: "image_descriptions"
            ));
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/normalized_images/*/imageDescription/captions/*/text",
                targetFieldName: "image_captions"
            ));
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/normalized_images/*/text",
                targetFieldName: "image_text"
            ));
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/mergedText",
                targetFieldName: "content"
            ));
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/language",
                targetFieldName: "language"
            ));
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/sentimentScore",
                targetFieldName: "sentiment"
            ));
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/locations",
                targetFieldName: "locations"
            ));
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/urls",
                targetFieldName: "links"
            ));
            outputMappings.Add(new FieldMapping(
                sourceFieldName: "/document/keyPhrases/*",
                targetFieldName: "key_phrases"
            ));
            
            // uncomment below to add custom skill field
            //outputMappings.Add(new FieldMapping(
            //    sourceFieldName: "/document/topWords",
            //    targetFieldName: "top_words"
            //));


            // Create the indexer
            Indexer indexer = new Indexer(
                name: IndexerName,
                dataSourceName: DataSourceName,
                targetIndexName: IndexName,
                skillsetName: SkillsetName,
                description: "Indexer",
                parameters: new IndexingParameters(
                    maxFailedItems: -1,
                    maxFailedItemsPerBatch: -1,
                    configuration: config),
                fieldMappings: fieldMappings,
                outputFieldMappings: outputMappings);

            try
            {
                bool exists = searchClient.Indexers.Exists(indexer.Name);

                if (exists)
                {
                    searchClient.Indexers.Delete(indexer.Name);
                }

                searchClient.Indexers.Create(indexer);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create the indexer\n Exception message: {0}\n", e.Message);
            }

            return indexer;
        }
        private static void CheckIndexerOverallStatus(SearchServiceClient searchClient)
        {
            try
            {
                // Get the index status
                IndexerExecutionInfo indexerExecutionInfo = searchClient.Indexers.GetStatus(IndexerName);

                switch (indexerExecutionInfo.Status)
                {
                    case IndexerStatus.Error:
                        Console.WriteLine("\nIndexer has error status. Check the Azure Portal to further understand the error.");
                        break;
                    case IndexerStatus.Running:
                        Console.WriteLine("\nIndexer is running");
                        break;
                    case IndexerStatus.Unknown:
                        Console.WriteLine("\nIndexer status is unknown");
                        break;
                    default:
                        Console.WriteLine("\nNo indexer information");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("\nFailed to get indexer overall status\n Exception message: {0}\n", e.Message);
            }
        }

        private static void ResetIndexer(SearchServiceClient searchClient)
        {
            searchClient.Indexers.Reset(IndexerName);
            searchClient.Indexers.Run(IndexerName);
        }

    }
}
