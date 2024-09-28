using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.SemKern
{
    #region LLM GENERATED

    //public class OllamaTextGenerationService : IOllamaTextGenerationService
    //{
    //    private readonly HttpClient _client;

    //    public string Endpoint { get; }

    //    public OllamaTextGenerationService(string endpoint)
    //    {
    //        _client = new HttpClient();
    //        Endpoint = endpoint;
    //    }

    //    public async Task<string> GenerateAsync(string text, CancellationToken cancellationToken = default)
    //    {
    //        var request = new
    //        {
    //            model = "ollama-model", // replace with the name of your Ollama model
    //            prompt = text,
    //            stream = false
    //        };

    //        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
    //        var response = await _client.PostAsync($"{Endpoint}/api/generate", content);
    //        var json = await response.Content.ReadAsStringAsync();
    //        dynamic result = JsonConvert.DeserializeObject(json);
    //        return result?.response;
    //    }

    //    public IReadOnlyDictionary<string, object?> Attributes => throw new NotImplementedException();





    //    public Task<IReadOnlyList<TextContent>> GetTextContentsAsync(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public IAsyncEnumerable<StreamingTextContent> GetStreamingTextContentsAsync(string prompt, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    //    {
    //        throw new NotImplementedException();
    //    }


    //}

    #endregion
}
