using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using search_client.Models;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Configuration;

namespace search_client.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }

        public string SearchTerms { get; set; } = "";
        public string SortOrder { get; set; } = "search.score()";

        public string FilterExpression { get; set; } = "";

        public DocumentSearchResult<SearchResult> resultList;

        public void OnGet()
        {
            if (Request.QueryString.HasValue){
                var queryString = QueryHelpers.ParseQuery(Request.QueryString.ToString());
                SearchTerms = queryString["search"];

                if (queryString.Keys.Contains("sort")){
                    SortOrder = queryString["sort"];
                }

                if (queryString.Keys.Contains("facet")){
                    FilterExpression = "author eq '" + queryString["facet"] + "'";
                    Console.WriteLine(FilterExpression);
                }
                else
                {
                    FilterExpression = "";
                }
                
                Console.WriteLine(Request.QueryString);

                 // Create a configuration using the appsettings file.
                IConfigurationBuilder _builder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
                IConfigurationRoot _configuration = _builder.Build();

                // Pull the values from the appsettings.json file.
                string searchServiceName = _configuration["SearchServiceName"];
                string queryApiKey = _configuration["SearchServiceQueryApiKey"];

                // Create a service and index client.
                SearchServiceClient _serviceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(queryApiKey));
                ISearchIndexClient  _indexClient = _serviceClient.Indexes.GetClient("margies-index-cs");

                var parameters = new SearchParameters{
                    Select = new[] { "url", "file_name", "author", "size", "last_modified" },
                    SearchMode = SearchMode.All,
                    HighlightFields = new[]{"content-3"},
                    Facets = new[] {"author"},
                    OrderBy = new[] {SortOrder},
                    Filter = FilterExpression
                };


                resultList  = _indexClient.Documents.Search<SearchResult>(SearchTerms, parameters);

                Console.Write(resultList);


            }
            else{
                SearchTerms="";
            }
        }


    }

}
