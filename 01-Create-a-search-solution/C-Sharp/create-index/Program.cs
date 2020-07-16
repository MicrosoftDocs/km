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
        public static void Main(string[] args)
        {
            // Create search service client
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            SearchServiceClient searchClient = CreateSearchServiceClient(configuration);

            // Create or Update the data source
            Console.WriteLine("Creating or updating the data source...");
            DataSource dataSource = CreateOrUpdateDataSource(searchClient, configuration);

            // Create the index
            //Console.WriteLine("Creating the index...");
            //Index index = CreateIndex(searchClient);

            // Create and run the indexer
            //Console.WriteLine("Creating and running the indexer...");
            //Indexer indexer = CreateIndexer(searchClient, dataSource, index);
            //Console.WriteLine("Check the indexer status...");
            //CheckIndexerOverallStatus(searchClient, indexer);

            // Add synonyms
            //Console.WriteLine("Adding synonyms...");
            //AddSynonyms(searchClient, index);
        }
        private static SearchServiceClient CreateSearchServiceClient(IConfigurationRoot configuration)
        {
            string searchServiceName = configuration["SearchServiceName"];
            string adminApiKey = configuration["SearchServiceAdminApiKey"];

            SearchServiceClient searchClient = new SearchServiceClient(searchServiceName, new SearchCredentials(adminApiKey));
            return searchClient;
        }
        private static DataSource CreateOrUpdateDataSource(SearchServiceClient serviceClient, IConfigurationRoot configuration)
        {
            DataSource dataSource = DataSource.AzureBlobStorage(
                name: "margies-docs",
                storageConnectionString: configuration["AzureBlobConnectionString"],
                containerName: "margies",
                description: "Documents in Margies website.");

            // The data source does not need to be deleted if it was already created
            // since we are using the CreateOrUpdate method
            try
            {
                serviceClient.DataSources.CreateOrUpdate(dataSource);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create or update the data source\n Exception message: {0}\n", e.Message);
                ExitProgram("Cannot continue without a data source");
            }

            return dataSource;
        }
         private static Index CreateIndex(SearchServiceClient searchClient)
        {
            var index = new Index()
            {
                Name = "margies-index",
                Fields = FieldBuilder.BuildForType<MargiesIndex>()
            };

            try
            {
                bool exists = searchClient.Indexes.Exists(index.Name);

                if (exists)
                {
                    searchClient.Indexes.Delete(index.Name);
                }

                searchClient.Indexes.Create(index);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create the index\n Exception message: {0}\n", e.Message);
                ExitProgram("Cannot continue without an index");
            }

            return index;
        }
        private static Indexer CreateIndexer(SearchServiceClient searchClient, DataSource dataSource, Index index)
        {
            IDictionary<string, object> config = new Dictionary<string, object>();
            config.Add(
                key: "dataToExtract",
                value: "contentAndMetadata");

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


            Indexer indexer = new Indexer(
                name: "margies-indexer",
                dataSourceName: dataSource.Name,
                targetIndexName: index.Name,
                description: "Margies Indexer",
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
                ExitProgram("Cannot continue without creating an indexer");
            }

            return indexer;
        }
        private static void CheckIndexerOverallStatus(SearchServiceClient searchClient, Indexer indexer)
        {
            try
            {
                IndexerExecutionInfo indexerExecutionInfo = searchClient.Indexers.GetStatus(indexer.Name);

                switch (indexerExecutionInfo.Status)
                {
                    case IndexerStatus.Error:
                        ExitProgram("Indexer has error status. Check the Azure Portal to further understand the error.");
                        break;
                    case IndexerStatus.Running:
                        Console.WriteLine("Indexer is running");
                        break;
                    case IndexerStatus.Unknown:
                        Console.WriteLine("Indexer status is unknown");
                        break;
                    default:
                        Console.WriteLine("No indexer information");
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to get indexer overall status\n Exception message: {0}\n", e.Message);
            }
        }

        private static void AddSynonyms(SearchServiceClient searchClient, Index index)
        {
            try
            {
                // Create a synonym map
                var synonymMap = new SynonymMap()
                {
                    Name = "locationsynonyms",
                    Synonyms = "SA,United States,America,United States of America\nUK,GB,United Kingdom,Great Britain,Britain\nUAE,United Arab Emirates,Emirates\n"
                };
                searchClient.SynonymMaps.CreateOrUpdate(synonymMap);

                // Apply the synonym map to the content field
                for (int i=0;i<index.Fields.Count; i++)
                {
                    if (index.Fields[i].Name == "content"){
                        index.Fields[i].SynonymMaps = new[] { "locationsynonyms" };
                    }
                }
                searchClient.Indexes.CreateOrUpdate(index);

            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to add synonyms\n Exception message: {0}\n", e.Message);
            }
        }

        private static void ExitProgram(string message)
        {
            Console.WriteLine("{0}", message);
            Console.WriteLine("Press any key to exit the program...");
            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}
