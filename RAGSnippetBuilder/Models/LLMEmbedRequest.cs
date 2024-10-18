using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    public record LLMEmbedRequest
    {
        public long UniqueID { get; init; }
        public string Snippet { get; init; }
        public string Description { get; init; }
        public string[] Questions { get; init; }
    }
}
