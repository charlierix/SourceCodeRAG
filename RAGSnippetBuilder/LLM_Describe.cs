using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using RAGSnippetBuilder.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    /// <summary>
    /// This will call out to an llm through ollama, and will be used to make descriptions of snippets
    /// </summary>
    /// <remarks>
    /// Searching a rag db of raw code with natural language descriptions gave bad results
    /// 
    /// This will be used to describe the snippets as natual language so the embeddings create closer
    /// matches
    /// </remarks>
    public class LLM_Describe
    {
        private readonly OllamaApiClient _client;
        private readonly OllamaChatCompletionService _service;

        // TODO: come up with multiple.  This one describes a snippet.  Others ideas:
        //  Come up possible questions that might be asked about a snippet
        //  Store each question as its own rag entry
        //
        //  Generate a list of tags (maybe sorted by relevance)
        //  A question asked will also be split into tags (maybe store as rag, maybe just strings, maybe both)
        //  Search each tag independently

        private readonly ChatHistory _chat_describe;
        private readonly int _systemcount_describe;

        public LLM_Describe(string url, string model_name)
        {
            _client = new OllamaApiClient(url, model_name);
            _service = new OllamaChatCompletionService(model_name, _client);

            // TODO: may want to change the last sentence.  Keep the fact that's its directly used in an embedding, so don't add ... anything more ...
            //_chat_describe = new ChatHistory("You are an assistant designed to summarize code snippets. Your task is to describe each given function or class in a concise and detailed manner using programming terms and concepts. The description should be a maximum of three sentences, focusing on the purpose, input parameters, output, and any key features of the function. Keep your description clear, precise, and relevant to its functionality. The output will be directly used for embedding, so it's important to use language that is likely to appear in related questions about the code.");
            _chat_describe = new ChatHistory("You are a helpful assistant that knows about AI.");
            _systemcount_describe = _chat_describe.Count;
        }

        public CodeDescription[] Describe(CodeFile code_file)
        {
            var retVal = new List<CodeDescription>();

            foreach (var snippet in code_file.Snippets)
            {
                retVal.Add(new CodeDescription()
                {
                    UniqueID = snippet.UniqueID,
                    Description = Describe(snippet.Text),
                });
            }

            return retVal.ToArray();
        }

        public string Describe(string snippet)
        {
            // Get rid of previous call
            while (_chat_describe.Count > _systemcount_describe)
                _chat_describe.RemoveAt(_chat_describe.Count - 1);


            //_chat_describe.AddUserMessage(snippet);
            _chat_describe.AddUserMessage("Hi, I'm looking for book suggestions");

            //var reply = _service.GetChatMessageContentAsync(_chat_describe).Result;

            var reply = _service.GetChatMessageContentAsync(_chat_describe);

            Task.WaitAll(reply);



            return null;
        }
    }
}
