using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    public record SettingsModel
    {
        public SettingsModel_LLM llm { get; init; }
    }

    public record SettingsModel_LLM
    {
        public string url { get; init; }

        public string model_describe { get; init; }
        public int max_threads_describe { get; init; }

        public string model_embed { get; init; }
        public int max_threads_embed { get; init; }
    }
}
