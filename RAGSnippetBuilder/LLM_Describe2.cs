using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    public class LLM_Describe2
    {
        //private readonly AsyncProcessor<string, string> _processor;

        //public LLM_Describe2(string url, string model_name, int maxConcurrency)
        //{
        //    OllamaApiClient client = new OllamaApiClient(url, model_name);
        //    OllamaChatCompletionService service = new OllamaChatCompletionService(model_name, client);

        //    _processor = new AsyncProcessor<string, string>(async snippet => await ProcessSnippetAsync(service, snippet), maxConcurrency: maxConcurrency);
        //}

        //private async Task<string> ProcessSnippetAsync(OllamaChatCompletionService service, string snippet)
        //{
        //    // Use the shared service to process the snippet asynchronously
        //    return await service.ProcessAsync(snippet);
        //}

        //public Task<string> DescribeAsync(string snippet)
        //{
        //    return _processor.ProcessAsync(snippet);
        //}
    }
}
