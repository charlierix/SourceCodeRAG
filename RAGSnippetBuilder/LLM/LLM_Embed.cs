using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using OpenAI.Embeddings;
using RAGSnippetBuilder.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.LLM
{
    public class LLM_Embed
    {
        private readonly AsyncProcessor<LLMEmbedRequest, LLMEmbedResult> _processor;

        public LLM_Embed(string url, string model, int thread_count = 1)
        {
            Func<Func<LLMEmbedRequest, Task<LLMEmbedResult>>> serviceFactory = () =>
            {
                var client = new OllamaApiClient(url, model);
                var service = new OllamaTextEmbeddingGenerationService(model, client);

                return async req => await Process_LLM(client, service, req);
            };

            _processor = new AsyncProcessor<LLMEmbedRequest, LLMEmbedResult>(serviceFactory, thread_count);
        }

        public LLMEmbedResult[] Embed(CodeFile code, LLMDescribeResult[] descriptions)
        {
            // Make sure that each item of llm result is for the same uniqueID
            ValidateSameUniqueIDs(descriptions);

            var results = new List<Task<LLMEmbedResult>>();

            foreach (var snippet in code.Snippets)
            {
                // This scan could be a bit expensive, but a code file shouldn't have more than a couple dozen functions - maybe a thousand in extreme cases
                var desc = descriptions.FirstOrDefault(o => o.Description.UniqueID ==  snippet.UniqueID)?.Description;
                if (desc == null)
                    throw new ApplicationException($"Couldn't find description for uniqueID: {snippet.UniqueID}, descriptions: {string.Join(", ", descriptions.Select(o => o.Description.UniqueID.ToString()))}");

                var question = descriptions.FirstOrDefault(o => o.Questions.UniqueID == snippet.UniqueID)?.Questions;
                if (question == null)
                    throw new ApplicationException($"Couldn't find questions for uniqueID: {snippet.UniqueID}, questions: {string.Join(", ", descriptions.Select(o => o.Questions.UniqueID.ToString()))}");

                LLMEmbedRequest request = new LLMEmbedRequest()
                {
                    UniqueID = snippet.UniqueID,
                    Snippet = snippet.Text,
                    Description = desc.Description,
                    Questions = question.Questions,
                };

                results.Add(_processor.ProcessAsync(request));
            }

            Task.WaitAll(results.ToArray());

            return results.
                Select(o => o.Result).
                ToArray();
        }

        /// <summary>
        /// Embeds each of the strings somewhat in parallel (the ID isn't needed, but makes the return object better)
        /// </summary>
        public LLMEmbedResult Embed(LLMEmbedRequest request)
        {
            return _processor.ProcessAsync(request).Result;
        }

        private static async Task<LLMEmbedResult> Process_LLM(OllamaApiClient client, OllamaTextEmbeddingGenerationService service, LLMEmbedRequest request)
        {
            var data = new List<string>();
            data.Add(request.Snippet);
            data.Add(request.Description);
            data.AddRange(request.Questions);

            var vectors = await service.GenerateEmbeddingsAsync(data.ToArray());

            return new LLMEmbedResult()
            {
                UniqueID = request.UniqueID,
                Snippet = vectors[0].ToArray(),
                Description = vectors[1].ToArray(),
                Questions = vectors.
                    Skip(2).
                    Select(o => o.ToArray()).
                    ToArray(),
            };
        }

        private static void ValidateSameUniqueIDs(LLMDescribeResult[] descriptions)
        {
            foreach (var desc in descriptions)
            {
                if (desc.Description.UniqueID != desc.Questions.UniqueID)
                    throw new ApplicationException($"LLMDescribeResult instance contains multiple uniqueIDs.  Description: {desc.Description.UniqueID}, Questions: {desc.Questions.UniqueID}");

                if (desc.Description.UniqueID != desc.Tags.UniqueID)
                    throw new ApplicationException($"LLMDescribeResult instance contains multiple uniqueIDs.  Description: {desc.Description.UniqueID}, Tags: {desc.Tags.UniqueID}");
            }
        }
    }
}
