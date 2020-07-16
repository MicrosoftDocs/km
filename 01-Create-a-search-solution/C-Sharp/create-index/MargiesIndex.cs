using System;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace margies.search
{
    [SerializePropertyNamesAsCamelCase]
    public class MargiesIndex
    {
        [System.ComponentModel.DataAnnotations.Key]
        [IsSearchable, IsSortable, IsFilterable]
        public string id { get; set; }

        [IsSearchable, IsFilterable]
        public string url { get; set; }

        [IsSearchable, IsFilterable, IsSortable]
        public string file_name { get; set; }

        [IsSearchable, IsFilterable, IsSortable, IsFacetable]
        public string author { get; set; }

        [IsSearchable, IsFilterable]
        public string content { get; set; }

        [IsSortable, IsFilterable]
        public int size { get; set; }

        [IsSortable, IsFilterable]
        public DateTime last_modified { get; set; }
    }
}
