using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using RAGSnippetBuilder.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.LLM
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
        public record LLMResults
        {
            public CodeDescription Description { get; init; }
            public CodeQuestions Questions { get; init; }
        }

        private readonly AsyncProcessor<CodeSnippet, LLMResults> _processor;

        public LLM_Describe(string url, string model, int thread_count = 1)
        {
            // This delegate gets called from a worker thread and is a chance to set up something that can process incoming requests
            Func<Func<CodeSnippet, Task<LLMResults>>> serviceFactory = () =>
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

                var chat_describe = new ChatHistory("You are an assistant designed to summarize code snippets. Your task is to describe each given function or class in a concise and detailed manner using programming terms and concepts. The description should be a maximum of three sentences, focusing on the purpose, input parameters, output, and any key features of the function. Keep your description clear, precise, and relevant to its functionality. The output will be directly used for embedding, so it's important to use language that is likely to appear in related questions about the code.");
                int system_count_describe = chat_describe.Count;

                // This likes to put tick marks ` around types or other identifiers.  Remove that in a post process so it's like it came from a chat textbox
                var chat_questions = new ChatHistory("You are an assistant designed to generate a list of potential questions about given code snippets. Your goal is to create a list of concise, standalone questions likely to be asked by developers who are familiar with the codebase that might be asked about the function or class, focusing on unique features. The questions should be focused on understanding the functionality and behavior of the provided code and are used to help find this function. Simple functions don't ned a lot of questions. If a question is vague or asking about whether something should be passed by val vs by ref, then it is of no use and should not be generated. Each of the questions will be fed into an embedding model, so only generate questions, do not engage in a conversation or dialogue. Format your response as a bullet-point list where each line starts with an asterisk (*).");
                int system_count_questions = chat_questions.Count;

                return async snippet => await Process_LLM(service, chat_describe, system_count_describe, chat_questions, system_count_questions, snippet);
            };

            _processor = new AsyncProcessor<CodeSnippet, LLMResults>(serviceFactory, thread_count);
        }

        public LLMResults[] Describe(CodeFile code_file)
        {
            var results = new List<Task<LLMResults>>();

            foreach (var snippet in code_file.Snippets)
                results.Add(_processor.ProcessAsync(snippet));

            Task.WaitAll(results.ToArray());

            return results.
                Select(o => o.Result).
                ToArray();
        }

        public double AverageCallTime_Milliseconds => _processor.AverageCallTime_Milliseconds;

        private static async Task<LLMResults> Process_LLM(OllamaChatCompletionService service, ChatHistory chat_describe, int system_count_describe, ChatHistory chat_questions, int system_count_questions, CodeSnippet snippet)
        {
            string description = await CallLLM(service, chat_describe, system_count_describe, snippet.Text);

            string questions = await CallLLM(service, chat_questions, system_count_questions, snippet.Text);
            string[] question_arr = ParseQuestionResponse(questions);

            return new LLMResults()
            {
                Description = new CodeDescription()
                {
                    UniqueID = snippet.UniqueID,
                    Description = description,
                },

                Questions = new CodeQuestions()
                {
                    UniqueID = snippet.UniqueID,
                    Questions = question_arr,
                },
            };
        }

        private static async Task<string> CallLLM(OllamaChatCompletionService service, ChatHistory chat, int system_count, string text)
        {
            // Get rid of previous call
            while (chat.Count > system_count)
                chat.RemoveAt(chat.Count - 1);

            // Add the new text in
            chat.AddUserMessage(text);

            var reply = await service.GetChatMessageContentAsync(chat);

            var replies = reply.Items.      // when testing, .Items was count of one.  Each item under it was a (token?) word or partial word
                Select(o => o.ToString());      // ToString looks like it joins all the subwords together properly into the full response

            return string.Join(Environment.NewLine + Environment.NewLine, replies);     // there was only one, but if there are multiple, a couple newlines seems like a good delimiter
        }

        private static string[] ParseQuestionResponse(string response)
        {
            string[] lines = response.
                Replace("`", "").       // it wraps bits of code with back ticks, which makes it easier to read, but won't match how a person types
                Replace("\r\n", "\n").
                Split('\n');

            var retVal = new List<string>();

            string current = "";

            foreach (string line in lines)
            {
                if (line.StartsWith("*"))
                {
                    current = current.Trim();
                    if (current != "")
                        retVal.Add(current);

                    current = line.Substring(1).Trim();
                }
                else if (current != "")
                {
                    current += ". " + line.Trim();
                }
                else
                {
                    current = line.Trim();
                }
            }

            current = current.Trim();
            if (current != "")
                retVal.Add(current);

            return retVal.ToArray();
        }
    }
}
