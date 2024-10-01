﻿using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using RAGSnippetBuilder.DAL;
using RAGSnippetBuilder.LLM;
using RAGSnippetBuilder.Models;
using RAGSnippetBuilder.ParseCode;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace RAGSnippetBuilder
{
    public partial class MainWindow : Window
    {
        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        #endregion

        #region Event Listeners

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

                if (txtDBFolder.Text == "")
                {
                    MessageBox.Show("Please select a folder for the sql db", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                else if (!Directory.Exists(txtDBFolder.Text))
                {
                    MessageBox.Show("SQL DB folder doesn't exist", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create a subfolder in the output
                string output_folder = System.IO.Path.Combine(txtOutputFolder.Text, $"{DateTime.Now:yyyyMMdd HHmmss} attempt1");
                Directory.CreateDirectory(output_folder);

                var dal = new DAL_SQLDB(txtDBFolder.Text);
                dal.TruncateTables();

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
                            WriteResults_ToDB(results, dal);
                            WriteResults_ToFile(output_folder, results);
                            break;
                    }
                }

                dal.FlushPending();

                MessageBox.Show("Finished", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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
        private void DescribeFunctions_Click(object sender, RoutedEventArgs e)
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


                // Create a subfolder in the output
                string folder_prefix = DateTime.Now.ToString("yyyyMMdd HHmmss");
                string output_folder_snippets = System.IO.Path.Combine(txtOutputFolder.Text, $"{folder_prefix} snippets");
                string output_folder_descriptions = System.IO.Path.Combine(txtOutputFolder.Text, $"{folder_prefix} descriptions");
                Directory.CreateDirectory(output_folder_snippets);
                Directory.CreateDirectory(output_folder_descriptions);


                var code_describer = new LLM_Describe(txtOllamaURL.Text, txtOllamaModel.Text);

                long token = 0;


                foreach (string filename in Directory.EnumerateFiles(txtSourceFolder.Text, "*", SearchOption.AllDirectories))
                {
                    FilePathInfo filepath = GetFilePathInfo(txtSourceFolder.Text, filename);

                    switch (System.IO.Path.GetExtension(filename).ToLower())
                    {
                        case ".swift":
                            var results = Parser_Swift.Parse(filepath, () => ++token);

                            CodeDescription[] descriptions = code_describer.Describe(results);

                            WriteResults_ToFile(output_folder_snippets, results);
                            WriteResults_ToFile(output_folder_descriptions, results.File, descriptions);
                            break;
                    }
                }
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
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            string json = JsonSerializer.Serialize(results, options);

            string filename = $"{results.File} {Guid.NewGuid()}.json";
            filename = System.IO.Path.Combine(output_folder, filename);

            File.WriteAllText(filename, json);
        }
        private static void WriteResults_ToFile(string output_folder, string source_filename, CodeDescription[] descriptions)
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = true,
            };

            string json = JsonSerializer.Serialize(new { descriptions }, options);

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