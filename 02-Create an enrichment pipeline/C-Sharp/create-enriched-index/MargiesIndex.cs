using System;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;

namespace margies.search
{

    [SerializePropertyNamesAsCamelCase]
    public struct Caption
    {
        [IsSearchable]
        public string text { get; set; }
        public double confidence { get; set; }
    }

    [SerializePropertyNamesAsCamelCase]
    public struct ImageDescription
    {
        [IsSearchable]
        public string[] tags { get; set; }
        public Caption[] captions { get; set; }
    }


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

        [IsSortable, IsFilterable]
        public int size { get; set; }

        [IsSortable, IsFilterable]
        public DateTime last_modified { get; set; }

        [IsFilterable]
        public string language { get; set; }

        [IsFilterable, IsSortable, IsFacetable]
        public double sentiment { get; set; }

        [IsSearchable]
        public string[] key_phrases { get; set; }

        [IsSearchable, IsFilterable]
        public string[] locations { get; set; }

        [IsSearchable, IsFilterable]
        public string[] links { get; set; }

        public ImageDescription[] image_descriptions  { get; set; }

        [IsSearchable, IsFilterable]
        public string[] image_captions { get; set; }

        [IsSearchable, IsFilterable]
        public string[] image_text { get; set; }

        [IsSearchable, IsFilterable]
        public string content { get; set; }

        // Uncomment below to add custom skill field
        //[IsSearchable, IsFilterable]
        //public string[] top_words { get; set; }
    }
}
