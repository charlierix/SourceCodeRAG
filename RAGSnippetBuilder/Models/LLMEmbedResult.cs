using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    public record LLMEmbedResult
    {
        public long UniqueID { get; init; }

        public float[] Snippet { get; init; }
        public float[] Description { get; init; }
        public float[][] Questions { get; init; }
    }
}
