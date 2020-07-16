using System;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Spatial;
using Newtonsoft.Json;

namespace search_client.Models
{
    public partial class SearchResult
    {
        [IsSearchable, IsFilterable]
        public string url { get; set; }

        [IsSearchable, IsFilterable, IsSortable]
        public string file_name { get; set; }

        [IsSearchable, IsFilterable, IsSortable, IsFacetable]
        public string author { get; set; }

        [IsSortable, IsFilterable]
        public int size { get; set; }

        [IsSortable, IsFilterable]
        public DateTime last_modified { get; set; }
    }
}