using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using RAGSnippetBuilder.DAL;
using RAGSnippetBuilder.LLM;
using RAGSnippetBuilder.Models;
using RAGSnippetBuilder.ParseCode;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Shapes;

namespace RAGSnippetBuilder
{
    public partial class MainWindow : Window
    {
        #region record: CodeQuestionWrapper

        // this is needed to deserialize
        private record CodeQuestionWrapper
        {
            public CodeQuestions[] questions { get; init; }
        }

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        #endregion

        #region Event Listeners

        // ******* Final *******

        private void ParseFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (txtSourceFolder.Text == "")
                {
                    MessageBox.Show("Please select a source folder", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (!Directory.Exists(txtSourceFolder.Text))
                {
                    MessageBox.Show("Source folder doesn't exist", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (txtOutputFolder.Text == "")
                {
                    MessageBox.Show("Please select an output folder", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (!Directory.Exists(txtOutputFolder.Text))
                {
                    MessageBox.Show("Output folder doesn't exist", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtOllamaThreads.Text, out int llm_threads))
                {
                    MessageBox.Show("Couldn't parse ollama threads as an integer", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create subfolders in the output
                string folder_prefix = DateTime.Now.ToString("yyyyMMdd HHmmss");

                string output_folder_snippets = System.IO.Path.Combine(txtOutputFolder.Text, $"{folder_prefix} snippets");
                string output_folder_descriptions = System.IO.Path.Combine(txtOutputFolder.Text, $"{folder_prefix} descriptions");
                string output_folder_questions = System.IO.Path.Combine(txtOutputFolder.Text, $"{folder_prefix} questions");
                string output_folder_tags = System.IO.Path.Combine(txtOutputFolder.Text, $"{folder_prefix} tags");
                string output_folder_sql = System.IO.Path.Combine(txtOutputFolder.Text, $"{folder_prefix} sql");

                Directory.CreateDirectory(output_folder_snippets);
                Directory.CreateDirectory(output_folder_descriptions);
                Directory.CreateDirectory(output_folder_questions);
                Directory.CreateDirectory(output_folder_tags);
                Directory.CreateDirectory(output_folder_sql);

                // Make sure the table is empty
                new DAL_SQLDB(output_folder_sql).TruncateTables();

                // Use this so the main thread doesn't get held up as bad (hopefully allowing llm writer to get more done)
                var dal = new DALTaskWrapper(output_folder_sql);

                // LLM caller
                var code_describer = new LLM_Describe(txtOllamaURL.Text, txtOllamaModel.Text, llm_threads);

                long uniqueID = 0;

                // TODO: make a progress bar
                //string[] filenames = Directory.GetFiles(txtSourceFolder.Text, "*", SearchOption.AllDirectories);

                foreach (string filename in Directory.EnumerateFiles(txtSourceFolder.Text, "*", SearchOption.AllDirectories))
                {
                    FilePathInfo filepath = GetFilePathInfo(txtSourceFolder.Text, filename);

                    switch (System.IO.Path.GetExtension(filename).ToLower())
                    {
                        case ".swift":
                            var results = Parser_Swift.Parse(filepath, () => ++uniqueID);

                            // NOTE: llm and dal run under their own threads and will pause this main thread if it gets too far ahead

                            var llm_results = code_describer.Describe(results);

                            foreach (CodeSnippet snippet in results.Snippets)
                                dal.Add(snippet, results.Folder, results.File);

                            WriteResults_ToFile(output_folder_snippets, results);
                            WriteResults_ToFile(output_folder_descriptions, results.File, llm_results.Select(o => o.Description).ToArray());
                            WriteResults_ToFile(output_folder_questions, results.File, llm_results.Select(o => o.Questions).ToArray());
                            WriteResults_ToFile(output_folder_tags, results.File, llm_results.Select(o => o.Tags).ToArray());
                            break;
                    }
                }

                dal.Finished();

                MessageBox.Show("Finished", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ******* Tests *******

        private void ParseLineUnitTests_Swift_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var delim_string = new Parse_Swift_Line.BlockDelimiters()
                {
                    Start = "\"",
                    End = "\"",
                    Escape = "\\",
                    IsMultiline = false,
                };

                var delim_string_singlequote = new Parse_Swift_Line.BlockDelimiters()
                {
                    Start = "'",
                    End = "'",
                    Escape = "\\",
                    IsMultiline = false,
                };

                var delim_string_multiline = new Parse_Swift_Line.BlockDelimiters()
                {
                    Start = "\"\"\"",
                    End = "\"\"\"",
                    Escape = null,
                    IsMultiline = true,
                };

                var delim_string_pound = new Parse_Swift_Line.BlockDelimiters()
                {
                    Start = "##\"",
                    End = "\"##",
                    Escape = null,
                    IsMultiline = false,
                };

                var delim_string_pound_multiline = new Parse_Swift_Line.BlockDelimiters()
                {
                    Start = "#\"\"\"",
                    End = "\"\"\"#",
                    Escape = null,
                    IsMultiline = true,
                };

                var delim_comment = new Parse_Swift_Line.BlockDelimiters()
                {
                    Start = @"\\",
                    End = null,
                    Escape = null,
                    IsMultiline = false,
                };

                var delim_comment_multiline = new Parse_Swift_Line.BlockDelimiters()
                {
                    Start = @"\*",
                    End = @"*\",
                    Escape = null,
                    IsMultiline = true,
                };

                var result1 = Parse_Swift_Line.ParseLine(@" @runtimeProperty(""category"", ""Final Conclusion"")", Parse_Swift_Line.SpanType.Other, null);
                var result2 = Parse_Swift_Line.ParseLine(@" @runtimeProperty('category', 'Final Conclusion')", Parse_Swift_Line.SpanType.Other, null);

                var result3a = Parse_Swift_Line.ParseLine("var multi = \"\"\"beginning of", Parse_Swift_Line.SpanType.Other, null);
                var result3b = Parse_Swift_Line.ParseLine("a multi", result3a.state, result3a.delimiters);
                var result3c = Parse_Swift_Line.ParseLine("line string\"\"\"", result3b.state, result3b.delimiters);
                var result3d = Parse_Swift_Line.ParseLine("regular code here", result3c.state, result3c.delimiters);

                var result4 = Parse_Swift_Line.ParseLine("pound_str = ##\"some text\"#still text\"## // and comment", Parse_Swift_Line.SpanType.Other, null);

                var result4a = Parse_Swift_Line.ParseLine("var multi_pound = #\"\"\"beginning of", Parse_Swift_Line.SpanType.Other, null);
                var result4b = Parse_Swift_Line.ParseLine("a pound multi and a bunch of \"\"\"\"\"\"\"\"\" in between", result4a.state, result4a.delimiters);
                var result4c = Parse_Swift_Line.ParseLine("line string\"\"\"#", result4b.state, result4b.delimiters);
                var result4d = Parse_Swift_Line.ParseLine("regular code here", result4c.state, result4c.delimiters);

                var result5 = Parse_Swift_Line.ParseLine(@"let there be code // and some comments", Parse_Swift_Line.SpanType.Other, null);

                var result6a = Parse_Swift_Line.ParseLine("some code /*beginning of", Parse_Swift_Line.SpanType.Other, null);
                var result6b = Parse_Swift_Line.ParseLine("a // multi", result6a.state, result6a.delimiters);
                var result6c = Parse_Swift_Line.ParseLine("line \" comment*/", result6b.state, result6b.delimiters);
                var result6d = Parse_Swift_Line.ParseLine("regular code here", result6c.state, result6c.delimiters);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LLM_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                #region LLM GENERATED

                //// Create a new kernel builder and configure it with the Ollama service
                //var builder = Kernel.CreateBuilder();

                //builder.Configure(config =>
                //{
                //    config.AddTextGenerationService("ollama", new OllamaTextGenerationService("<your-ollama-endpoint>"));
                //});

                //// Build the kernel
                //var kernel = builder.Build();

                //// Define a semantic function in natural language
                //string promptTemplate = "Write a poem about {{topic}}";
                //var function = kernel.CreateFunctionFromPrompt(promptTemplate);

                //// Call the semantic function with input data
                //var result = await kernel.RunAsync(function, new { topic = "the ocean" });

                //// Print the output of the model
                //Console.WriteLine(result.Result);

                #endregion

                //var client = new OllamaApiClient(txtOllamaURL.Text, txtOllamaModel.Text);
                OllamaApiClient client = new OllamaApiClient(txtOllamaURL.Text, txtOllamaModel.Text);

                //var chatService = new OllamaChatCompletionService(txtOllamaModel.Text, client);
                OllamaChatCompletionService chatService = new OllamaChatCompletionService(txtOllamaModel.Text, client);

                //var chatHistory = new ChatHistory("You are a helpful assistant that knows about AI.");
                ChatHistory chatHistory = new ChatHistory("You are a helpful assistant that knows about AI.");

                chatHistory.AddUserMessage("Hi, I'm looking for book suggestions");

                var reply = await chatService.GetChatMessageContentAsync(chatHistory);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AsyncProcessorTestA_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                //Func<Func<string, Task<string>>> serviceFactory = () =>
                //{
                //    OllamaApiClient client = new OllamaApiClient(url, model_name);
                //    OllamaChatCompletionService service = new OllamaChatCompletionService(client);
                //    return async snippet => await service.ProcessAsync(snippet); // Return an anonymous delegate that uses the service object to process the input asynchronously
                //};


                // This delegate gets called from a worker thread and is a chance to set up something that can process incoming requests
                // (this is where the llm caller client would be set up)
                Func<Func<string, Task<string>>> serviceFactory = () =>
                {
                    string worker_id = Guid.NewGuid().ToString();
                    return async snippet => await Process(worker_id + " | " + snippet);
                };

                var processor = new AsyncProcessor<string, string>(serviceFactory, 2);

                string input = Guid.NewGuid().ToString();

                var tasks = new List<Task<string>>();

                for (int i = 0; i < 12; i++)        // these 12 work items will be randomly distributed across the available workers (2)
                    tasks.Add(processor.ProcessAsync(input));

                Task.WaitAll(tasks.ToArray());

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void AsyncProcessorTestB_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string url = txtOllamaURL.Text;
                string model = txtOllamaModel.Text;

                // This delegate gets called from a worker thread and is a chance to set up something that can process incoming requests
                Func<Func<string, Task<string>>> serviceFactory = () =>
                {
                    var client = new OllamaApiClient(url, model);
                    var service = new OllamaChatCompletionService(model, client);
                    var chat = new ChatHistory("You are a helpful assistant that knows about AI.");
                    int system_count = chat.Count;

                    return async snippet => await Process_LLM(service, chat, system_count, snippet);
                };

                var processor = new AsyncProcessor<string, string>(serviceFactory, 1);

                var tasks = new List<Task<string>>();

                tasks.Add(processor.ProcessAsync("Have you read any good books lately?"));
                tasks.Add(processor.ProcessAsync("What's the meaning of life?"));
                tasks.Add(processor.ProcessAsync("When did Bob get here?"));

                Task.WaitAll(tasks.ToArray());

                string[] results = tasks.
                    Select(o => o.Result).
                    ToArray();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ChromaTest_Click(object sender, RoutedEventArgs e)
        {
            string COLLECTION_NAME = "test1";

            try
            {
                if (txtOutputFolder.Text == "")
                {
                    MessageBox.Show("Please select an output folder", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (!Directory.Exists(txtOutputFolder.Text))
                {
                    MessageBox.Show("Output folder doesn't exist", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string folder_prefix = DateTime.Now.ToString("yyyyMMdd HHmmss");

                string output_folder_chroma = System.IO.Path.Combine(txtOutputFolder.Text, $"{folder_prefix} sql");

                Directory.CreateDirectory(output_folder_chroma);


                //https://learn.microsoft.com/en-us/dotnet/api/microsoft.semantickernel.connectors.chroma?view=semantic-kernel-dotnet

                // in python, inserts were done with collection class, but in c#, it seems to be client.upsertembeddings
                // gets are client.queryembeddings

                // I wonder if it's:
                //  client = new
                //  client.getcollection (or createcollection)
                //  client.upsert (or query)


                // there's also ChromaMemoryStore, but I doubt that saves to file


                // ----------- client -----------
                //https://learn.microsoft.com/en-us/dotnet/api/microsoft.semantickernel.connectors.chroma.chromaclient?view=semantic-kernel-dotnet

                //public class ChromaClient : Microsoft.SemanticKernel.Connectors.Chroma.IChromaClient
                //public ChromaClient (System.Net.Http.HttpClient httpClient, string? endpoint = default, Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = default);

                //public System.Threading.Tasks.Task CreateCollectionAsync (string collectionName, System.Threading.CancellationToken cancellationToken = default);

                //public System.Threading.Tasks.Task<Microsoft.SemanticKernel.Connectors.Chroma.ChromaCollectionModel?> GetCollectionAsync (string collectionName, System.Threading.CancellationToken cancellationToken = default);



                // ----------- collection -----------
                //https://learn.microsoft.com/en-us/dotnet/api/microsoft.semantickernel.connectors.chroma.chromacollectionmodel?view=semantic-kernel-dotnet



                // ----------- embedding -----------
                //https://ollama.com/blog/embedding-models



                // -----------------------------------

                var client = new ChromaClient("http something");        // this only takes endpoints, no way to specify a local db
                //var client = new persist      // there is no persistent client like there is in python

                try { await client.DeleteCollectionAsync(COLLECTION_NAME); }
                catch (Exception) { }

                await client.CreateCollectionAsync(COLLECTION_NAME);






                string embedding_model_name = "mxbai-embed-large";

                var embedding_client = new OllamaApiClient(txtOllamaURL.Text, embedding_model_name);

                var embedding_model = new OllamaTextEmbeddingGenerationService(embedding_model_name, embedding_client);
                var vectors = await embedding_model.GenerateEmbeddingsAsync(["hello", "there", "everybody"]);







                string[] ids = ["1", "2", "3"];


                await client.UpsertEmbeddingsAsync(COLLECTION_NAME, ids, vectors.ToArray());






                // When searching:  it looks like cosine similarity is better than distance in high dimensions, but just do both in different threads

                var vector2 = await embedding_model.GenerateEmbeddingsAsync(["greetings"]);


            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void ChromaTest2_Click(object sender, RoutedEventArgs e)
        {
            string COLLECTION_NAME = "test2";

            try
            {
                string embedding_model_name = "mxbai-embed-large";

                var embedding_client = new OllamaApiClient(txtOllamaURL.Text, embedding_model_name);

                var embedding_model = new OllamaTextEmbeddingGenerationService(embedding_model_name, embedding_client);
                var vectors = await embedding_model.GenerateEmbeddingsAsync(["hello", "there", "everybody"]);

                string[] ids = ["1", "2", "3"];






            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Flask_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = @"D:\!dev_repos\SourceCodeRAG\ChromaTest2\.venv\Scripts\python.exe",
                    Arguments = @"D:\!dev_repos\SourceCodeRAG\ChromaTest2\basic_flask.py",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                // Start the Python script and capture its output
                using (var process = new System.Diagnostics.Process() { StartInfo = startInfo })
                {
                    process.Start();


                    //string output = process.StandardOutput.ReadToEnd();       // these lock up
                    //string error = process.StandardError.ReadToEnd();

                    // Figure out the url
                    //string url = null;
                    //var match = Regex.Match(output, @"\* Running on (?<url>http://\d+.\d+.\d+.\d+:\d+)");
                    //if (match.Success)
                    //    url = match.Groups["url"].Value;
                    //else
                    //    throw new ApplicationException("Couldn't find url");



                    // Read the output asynchronously
                    process.BeginOutputReadLine();
                    process.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);

                    // For some reason, half the messages are coming across as errors, even though they aren't errors
                    process.BeginErrorReadLine();
                    process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);


                    // TODO: need to spin this thread until some command
                    while (true)
                        await Task.Delay(1000);


                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private async void Flask2_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = @"D:\!dev_repos\SourceCodeRAG\ChromaTest2\.venv\Scripts\python.exe",
                    Arguments = @"D:\!dev_repos\SourceCodeRAG\ChromaTest2\basic_flask.py",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                DateTime start_time = DateTime.UtcNow;
                string url = null;

                var parse_line = new Action<object, DataReceivedEventArgs>((sender, args) =>
                {
                    var match = Regex.Match(args.Data, @"\* Running on (?<url>http://\d+.\d+.\d+.\d+:\d+)");
                    if (match.Success)
                        url = match.Groups["url"].Value;
                });

                // Start the Python script and capture its output
                using (var process = new System.Diagnostics.Process() { StartInfo = startInfo })
                {
                    // Read the output asynchronously
                    process.OutputDataReceived += (sender, args) => parse_line(sender, args);
                    process.ErrorDataReceived += (sender, args) => parse_line(sender, args);        // For some reason, half the messages are coming across as errors, even though they aren't errors

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();


                    // TODO: need to spin this thread until some command
                    while (true)
                    {
                        DateTime now = DateTime.UtcNow;
                        double elapsed_seconds = (now - start_time).TotalSeconds;

                        if (elapsed_seconds > 12)
                        {
                            // Send Ctrl+C signal to the Python script
                            process.StandardInput.WriteLine("\u0003");  // This is ASCII code for Ctrl+C
                            break;
                        }

                        if (url == null && elapsed_seconds > 3)
                            throw new ApplicationException("Never saw the url");

                        await Task.Delay(1000);
                    }

                    while(!process.WaitForExit(200))
                        await Task.Delay(1000);
                }

                MessageBox.Show("Finished", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Private Methods

        private static FilePathInfo GetFilePathInfo(string source_folder, string filename)
        {
            string filename_only = System.IO.Path.GetFileName(filename);

            string remaining = System.IO.Path.GetDirectoryName(filename);
            remaining = System.IO.Path.GetRelativePath(source_folder, remaining);

            if (remaining == ".")       // it uses . when it's the same folder
                remaining = "";

            return new FilePathInfo()
            {
                SourceFolder = source_folder,
                FullFilename = filename,

                File = filename_only,
                Folder = remaining,
            };
        }

        private static void WriteResults_ToDB(CodeFile results, DAL_SQLDB dal)
        {
            foreach (CodeSnippet snippet in results.Snippets)
                dal.AddSnippet(snippet, results.Folder, results.File);
        }

        private static void WriteResults_ToFile(string output_folder, CodeFile results)
        {
            WriteResults_ToFile_DoIt(output_folder, results.File, results);
        }
        private static void WriteResults_ToFile(string output_folder, string source_filename, CodeDescription[] descriptions)
        {
            WriteResults_ToFile_DoIt(output_folder, source_filename, new { descriptions });
        }
        private static void WriteResults_ToFile(string output_folder, string source_filename, CodeQuestions[] questions)
        {
            WriteResults_ToFile_DoIt(output_folder, source_filename, new { questions });
        }
        private static void WriteResults_ToFile(string output_folder, string source_filename, CodeTags[] tags)
        {
            WriteResults_ToFile_DoIt(output_folder, source_filename, new { tags });
        }

        private static void WriteResults_ToFile_DoIt(string output_folder, string source_filename, object value)
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            string json = JsonSerializer.Serialize(value, options);

            string filename = $"{source_filename} {Guid.NewGuid()}.json";
            filename = System.IO.Path.Combine(output_folder, filename);

            File.WriteAllText(filename, json);
        }

        private static Task<string> Process(string input)
        {
            return Task.Run(() =>
            {
                Thread.Sleep(1500);
                return input + " | " + Guid.NewGuid().ToString();
            });
        }

        private static async Task<string> Process_LLM(OllamaChatCompletionService service, ChatHistory chat, int system_count, string input)
        {
            // Get rid of previous call
            while (chat.Count > system_count)
                chat.RemoveAt(chat.Count - 1);

            chat.AddUserMessage(input);

            var reply = await service.GetChatMessageContentAsync(chat);

            var replies = reply.Items.      // when testing, .Items was count of one.  Each item under it was a (token?) word or partial word
                Select(o => o.ToString());      // ToString looks like it joins all the subwords together properly into the full response

            return string.Join(Environment.NewLine + Environment.NewLine, replies);     // there was only one, but if there are multiple, a couple newlines seems like a good delimiter
        }

        #endregion
    }
}