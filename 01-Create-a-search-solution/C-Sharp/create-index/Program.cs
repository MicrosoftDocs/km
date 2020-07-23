using System;
using System.Collections.Generic;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Configuration;
using Index = Microsoft.Azure.Search.Models.Index;

namespace margies.search

{
    class Program
    {
        private const string DataSourceName = "margies-docs-cs";
        private const string ContainerName = "margies";
        private const string IndexName = "margies-index-cs";
        private const string IndexerName = "margies-indexer-cs";
        private const string SynonymMapName = "margies-synonyms-cs";

        public static void Main(string[] args)
        {
            // Get config settings from AppSettings
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            string searchServiceName = configuration["SearchServiceName"];
            string adminApiKey = configuration["SearchServiceAdminApiKey"];
            string blobConnectionString = configuration["AzureBlobConnectionString"];

            // Create search service client
            SearchServiceClient searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(adminApiKey));

            // Loop until the user presses "q"
            Boolean quit = false;
            while(quit == false){
                Console.WriteLine("What do you want to do?");
                Console.WriteLine("1: Create a Data Source");
                Console.WriteLine("2: Create an Index");
                Console.WriteLine("3: Create and run an Indexer");
                Console.WriteLine("4: Add synonyms");
                Console.WriteLine("q: Quit");
                var key = Console.ReadKey();
                switch(key.KeyChar.ToString()){
                    case "1":
                        CreateOrUpdateDataSource(searchClient, blobConnectionString);
                        break;
                    case "2":
                        CreateIndex(searchClient);
                        break;
                    case "3":
                        Indexer indexer = CreateIndexer(searchClient);
                        CheckIndexerOverallStatus(searchClient, indexer);
                        break;
                    case "4":
                        AddSynonyms(searchClient);
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
            Console.WriteLine("\nCreating or updating the data source...");

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
         private static void CreateIndex(SearchServiceClient searchClient)
        {
            Console.WriteLine("\nCreating index...");

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

            // Specify indexer configuration (we want to extract content and metadata)
            IDictionary<string, object> config = new Dictionary<string, object>();
            config.Add(
                key: "dataToExtract",
                value: "contentAndMetadata");

            // Map the extracted data values to the index fields
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
                sourceFieldName: "content",
                targetFieldName: "content"));
            fieldMappings.Add(new FieldMapping(
                sourceFieldName: "metadata_storage_size",
                targetFieldName: "size"));
            fieldMappings.Add(new FieldMapping(
                sourceFieldName: "metadata_storage_last_modified",
                targetFieldName: "last_modified"));


            // Create the indexer
            Indexer indexer = new Indexer(
                name: IndexerName,
                dataSourceName: DataSourceName,
                targetIndexName: IndexName,
                description: "Indexer",
                parameters: new IndexingParameters(
                    maxFailedItems: -1,
                    maxFailedItemsPerBatch: -1,
                    configuration: config),
                fieldMappings: fieldMappings);

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
        private static void CheckIndexerOverallStatus(SearchServiceClient searchClient, Indexer indexer)
        {
            try
            {
                // Get the index status
                IndexerExecutionInfo indexerExecutionInfo = searchClient.Indexers.GetStatus(indexer.Name);

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

        private static void AddSynonyms(SearchServiceClient searchClient)
        {
            Console.WriteLine("\nCreating synonym map...");
            try
            {
                // Create a synonym map
                var synonymMap = new SynonymMap()
                {
                    Name = SynonymMapName,
                    Synonyms = "SA,United States,America,United States of America\nUK,GB,United Kingdom,Great Britain,Britain\nUAE,United Arab Emirates,Emirates\n"
                };
                searchClient.SynonymMaps.CreateOrUpdate(synonymMap);

                // Get the index
                Index index = searchClient.Indexes.Get(IndexName);

                // Apply the synonym map to the content field
                for (int i=0;i<index.Fields.Count; i++)
                {
                    if (index.Fields[i].Name == "content"){
                        index.Fields[i].SynonymMaps = new[] { SynonymMapName };
                    }
                }
                searchClient.Indexes.CreateOrUpdate(index);

            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to add synonyms\n Exception message: {0}\n", e.Message);
            }
        }
    }
}
