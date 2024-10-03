using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    public record LLMResults
    {
        public CodeDescription Description { get; init; }
        public CodeQuestions Questions { get; init; }
        public CodeTags Tags { get; init; }
    }
}
