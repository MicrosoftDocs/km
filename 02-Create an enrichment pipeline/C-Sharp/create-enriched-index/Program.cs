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
        private static string customSkillUri;

        public static void Main(string[] args)
        {
            // Get config settings from AppSettings
            IConfigurationBuilder builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            IConfigurationRoot configuration = builder.Build();
            searchServiceName = configuration["SearchServiceName"];
            adminApiKey = configuration["SearchServiceAdminApiKey"];
            cognitiveServicesKey = configuration["CognitiveServicesApiKey"];
            blobConnectionString = configuration["AzureBlobConnectionString"];
            customSkillUri = configuration["AzureFunctionUri"];

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
                Console.WriteLine("5: Reset and re-run Indexer");
                Console.WriteLine("q: Quit");
                var key = Console.ReadKey();
                switch(key.KeyChar.ToString()){
                    case "1":
                        CreateOrUpdateDataSource(searchClient, blobConnectionString);
                        break;
                    case "2":
                        CreateSkillset(searchClient, cognitiveServicesKey);
                        break;
                    case "3":
                        CreateIndex(searchClient);
                        break;
                    case "4":
                        Indexer indexer = CreateIndexer(searchClient);
                        CheckIndexerOverallStatus(searchClient);
                        break;
                    case "5":
                        ResetIndexer(searchClient);
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

        private static void CreateSkillset(SearchServiceClient searchClient, string cognitiveServicesKey)
        {
            Console.WriteLine("\nDefining skills...");
            // Create a list for the skills we need
            List<Skill> skills = new List<Skill>();

            // Language detection skill
            Console.WriteLine("  - Language detection");
            // inputs
            List<InputFieldMappingEntry> languageInputs = new List<InputFieldMappingEntry>();
            languageInputs.Add(new InputFieldMappingEntry(
                name: "text",
                source: "/document/content"));
            // outputs
            List<OutputFieldMappingEntry> languageOutputs = new List<OutputFieldMappingEntry>();
            languageOutputs.Add(new OutputFieldMappingEntry(
                name: "languageCode",
                targetName: "language"));
            // Create skill
            LanguageDetectionSkill languageDetectionSkill = new LanguageDetectionSkill(
                name: "get-language",
                description: "Detect the language used in the document",
                context: "/document",
                inputs: languageInputs,
                outputs: languageOutputs);
            // Add to list
            skills.Add(languageDetectionSkill);

            // Image Analysis skill
            Console.WriteLine("  - Image analysis");
            // inputs
            List<InputFieldMappingEntry> imageAnalysisInputs = new List<InputFieldMappingEntry>();
            imageAnalysisInputs.Add(new InputFieldMappingEntry(
                name: "image",
                source: "/document/normalized_images/*"));
            // Visual features to extract
            List<VisualFeature> features = new List<VisualFeature>();
            features.Add(VisualFeature.Description);
            // outputs
            List<OutputFieldMappingEntry> imageAnalysisOutputs = new List<OutputFieldMappingEntry>();
            imageAnalysisOutputs.Add(new OutputFieldMappingEntry(
                name: "description",
                targetName: "imageDescription"));
            // Create skill
            ImageAnalysisSkill imageAnalysisSkill = new ImageAnalysisSkill(
                name: "get-image-descriptions",
                description: "Generate descriptions of images.",
                context: "/document/normalized_images/*",
                visualFeatures: features,
                inputs: imageAnalysisInputs,
                outputs:imageAnalysisOutputs);
            //Add to list
            skills.Add(imageAnalysisSkill);

            // OCR skill
            Console.WriteLine("  - OCR");
            // inputs
            List<InputFieldMappingEntry> ocrInputs = new List<InputFieldMappingEntry>();
            ocrInputs.Add(new InputFieldMappingEntry(
                name: "image",
                source: "/document/normalized_images/*"));
            // outputs
            List<OutputFieldMappingEntry> ocrOutputs = new List<OutputFieldMappingEntry>();
            ocrOutputs.Add(new OutputFieldMappingEntry(
                name: "text",
                targetName: "text"));
            // Create skill
            OcrSkill ocrSkill = new OcrSkill(
                name: "get-image-text",
                description: "Use OCR to extract text from images.",
                context: "/document/normalized_images/*",
                inputs: ocrInputs,
                outputs: ocrOutputs,
                shouldDetectOrientation: true);
            //Add to list
            skills.Add(ocrSkill);

            // Merge skill
            Console.WriteLine("  - Merge");
            // inputs
            List<InputFieldMappingEntry> mergeInputs = new List<InputFieldMappingEntry>();
            mergeInputs.Add(new InputFieldMappingEntry(
                name: "text",
                source: "/document/content"));
            mergeInputs.Add(new InputFieldMappingEntry(
                name: "itemsToInsert",
                source: "/document/normalized_images/*/text"));
            mergeInputs.Add(new InputFieldMappingEntry(
                name: "offsets",
                source: "/document/normalized_images/*/contentOffset"));
            // outputs
            List<OutputFieldMappingEntry> mergeOutputs = new List<OutputFieldMappingEntry>();
            mergeOutputs.Add(new OutputFieldMappingEntry(
                name: "mergedText",
                targetName: "mergedText"));
            // Create skill
            MergeSkill mergeSkill = new MergeSkill(
                name: "merge-text",
                description: "Create merged_text which includes all the textual representation of each image inserted at the right location in the content field.",
                context: "/document",
                inputs: mergeInputs,
                outputs: mergeOutputs,
                insertPreTag: "[",
                insertPostTag: "]");
            //Add to list
            skills.Add(mergeSkill);

            // Sentiment skill
            Console.WriteLine("  - Sentiment");
            // inputs
            List<InputFieldMappingEntry> sentimentInputs = new List<InputFieldMappingEntry>();
            sentimentInputs.Add(new InputFieldMappingEntry(
                name: "text",
                source: "/document/mergedText"));
            sentimentInputs.Add(new InputFieldMappingEntry(
                name: "languageCode",
                source: "/document/language"));
            // outputs
            List<OutputFieldMappingEntry> sentimentOutputs = new List<OutputFieldMappingEntry>();
            sentimentOutputs.Add(new OutputFieldMappingEntry(
                name: "score",
                targetName: "sentimentScore"));
            // Create skill
            SentimentSkill sentimentSkill = new SentimentSkill(
                name: "get-sentiment",
                description: "Detect sentiment.",
                context: "/document",
                inputs: sentimentInputs,
                outputs: sentimentOutputs);
            //Add to list
            skills.Add(sentimentSkill);

            // Entity recognition skill
            Console.WriteLine("  - Text entities");
            // inputs
            List<InputFieldMappingEntry> entityInputs = new List<InputFieldMappingEntry>();
            entityInputs.Add(new InputFieldMappingEntry(
                name: "text",
                source: "/document/mergedText"));
            entityInputs.Add(new InputFieldMappingEntry(
                name: "languageCode",
                source: "/document/language"));
            // Categories to extract
            List<EntityCategory> entityCategories = new List<EntityCategory>();
            entityCategories.Add(EntityCategory.Location);
            entityCategories.Add(EntityCategory.Url);
            // outputs
            List<OutputFieldMappingEntry> entityOutputs = new List<OutputFieldMappingEntry>();
            entityOutputs.Add(new OutputFieldMappingEntry(
                name: "locations",
                targetName: "locations"));
            entityOutputs.Add(new OutputFieldMappingEntry(
                name: "urls",
                targetName: "urls"));
            // Create skill
            EntityRecognitionSkill entitySkill = new EntityRecognitionSkill(
                name: "get-text-entities",
                description: "Extract locations and URLs",
                context: "/document",
                categories: entityCategories,
                inputs: entityInputs,
                outputs: entityOutputs);
            //Add to list
            skills.Add(entitySkill);

            // Key phrases skill
            Console.WriteLine("  - Key phrases");
            // inputs
            List<InputFieldMappingEntry> keyphraseInputs = new List<InputFieldMappingEntry>();
            keyphraseInputs.Add(new InputFieldMappingEntry(
                name: "text",
                source: "/document/mergedText"));
            keyphraseInputs.Add(new InputFieldMappingEntry(
                name: "languageCode",
                source: "/document/language"));
            // outputs
            List<OutputFieldMappingEntry> keyphraseOutputs = new List<OutputFieldMappingEntry>();
            keyphraseOutputs.Add(new OutputFieldMappingEntry(
                name: "keyPhrases",
                targetName: "keyPhrases"));
            // Create skill
            KeyPhraseExtractionSkill keyphraseSkill = new KeyPhraseExtractionSkill(
                name: "get-key-phrases",
                description: "Extract key phrases.",
                context: "/document",
                inputs: keyphraseInputs,
                outputs: keyphraseOutputs);
            //Add to list
            skills.Add(keyphraseSkill);


            // Uncomment below to add custom skill
            //skills.Add(CreateCustomSkill());


            // Create skillset
            Console.WriteLine("\nCreating skillset...\n");
            Skillset skillset = new Skillset(
                name: SkillsetName,
                description: "Extract and enrich data",
                skills: skills,
                cognitiveServices: new CognitiveServicesByKey(
                    cognitiveServicesKey,
                    "Cognitive Services account"));

            try
            {
                searchClient.Skillsets.CreateOrUpdate(skillset);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create the skillset\n Exception message: {0}\n", e.Message);
            }
        }

        private static WebApiSkill CreateCustomSkill()
        {
            Console.WriteLine("  - Custom skill");
            // inputs
            List<InputFieldMappingEntry> customInputs = new List<InputFieldMappingEntry>();
            customInputs.Add(new InputFieldMappingEntry(
                name: "text",
                source: "/document/mergedText"));
            customInputs.Add(new InputFieldMappingEntry(
                name: "language",
                source: "/document/language"));
            // outputs
            List<OutputFieldMappingEntry> customOutputs = new List<OutputFieldMappingEntry>();
            customOutputs.Add(new OutputFieldMappingEntry(
                name: "text",
                targetName: "topWords"));
            // Create skill
            WebApiSkill customSkill = new WebApiSkill(
                name: "get-top-words",
                description: "custom skill to get top 10 most frequent words.",
                uri: customSkillUri,
                context: "/document",
                inputs: customInputs,
                outputs: customOutputs);

            return customSkill;
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
