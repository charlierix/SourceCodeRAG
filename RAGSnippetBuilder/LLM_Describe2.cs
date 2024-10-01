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
    public class LLM_Describe2
    {
        public record LLMResults
        {
            public CodeDescription Description { get; init; }
        }


        
        private readonly AsyncProcessor<CodeSnippet, CodeDescription> _processor;

        public LLM_Describe2(string url, string model, int thread_count = 1)
        {
            // This delegate gets called from a worker thread and is a chance to set up something that can process incoming requests
            Func<Func<CodeSnippet, Task<CodeDescription>>> serviceFactory = () =>
            {
                var client = new OllamaApiClient(url, model);
                var service = new OllamaChatCompletionService(model, client);


                // TODO: come up with multiple.  This one describes a snippet.  Others ideas:
                //  Come up possible questions that might be asked about a snippet
                //  Store each question as its own rag entry
                //
                //  Generate a list of tags (maybe sorted by relevance)
                //  A question asked will also be split into tags (maybe store as rag, maybe just strings, maybe both)
                //  Search each tag independently


                // TODO: may want to change the last sentence.  Keep the fact that's its directly used in an embedding, so don't add ... anything more ...

                var chat = new ChatHistory("You are an assistant designed to summarize code snippets. Your task is to describe each given function or class in a concise and detailed manner using programming terms and concepts. The description should be a maximum of three sentences, focusing on the purpose, input parameters, output, and any key features of the function. Keep your description clear, precise, and relevant to its functionality. The output will be directly used for embedding, so it's important to use language that is likely to appear in related questions about the code.");
                int system_count = chat.Count;


                // TODO: when there are multiple, call them one at a time using task.continuewith, put all the results in LLMResults object

                return async snippet => await Process_LLM(service, chat, system_count, snippet);
            };

            _processor = new AsyncProcessor<CodeSnippet, CodeDescription>(serviceFactory, thread_count);
        }

        public CodeDescription[] Describe(CodeFile code_file)
        {
            var descriptions = new List<Task<CodeDescription>>();

            foreach (var snippet in code_file.Snippets)
                descriptions.Add(_processor.ProcessAsync(snippet));

            Task.WaitAll(descriptions.ToArray());

            return descriptions.
                Select(o => o.Result).
                ToArray();
        }

        public double AverageCallTime_Milliseconds => _processor.AverageCallTime_Milliseconds;

        private static async Task<CodeDescription> Process_LLM(OllamaChatCompletionService service, ChatHistory chat, int system_count, CodeSnippet snippet)
        {
            // Get rid of previous call
            while (chat.Count > system_count)
                chat.RemoveAt(chat.Count - 1);

            chat.AddUserMessage(snippet.Text);

            var reply = await service.GetChatMessageContentAsync(chat);

            var replies = reply.Items.      // when testing, .Items was count of one.  Each item under it was a (token?) word or partial word
                Select(o => o.ToString());      // ToString looks like it joins all the subwords together properly into the full response

            string description = string.Join(Environment.NewLine + Environment.NewLine, replies);     // there was only one, but if there are multiple, a couple newlines seems like a good delimiter

            return new CodeDescription()
            {
                UniqueID = snippet.UniqueID,
                Description = description,
            };
        }
    }
}
