using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    public record CodeTags
    {
        // Matches unique ID in CodeSnippet
        public long UniqueID { get; init; }

        public string[] Tags { get; init; }
    }
}
