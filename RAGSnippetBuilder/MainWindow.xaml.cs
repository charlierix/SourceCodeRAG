using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using OllamaSharp;
using RAGSnippetBuilder.Models;
using System;
using System.IO;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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


                // Iterate files recursively (stop at 12) for now

                // TODO: make a progress bar
                //string[] filenames = Directory.GetFiles(txtSourceFolder.Text, "*", SearchOption.AllDirectories);

                foreach (string filename in Directory.EnumerateFiles(txtSourceFolder.Text, "*", SearchOption.AllDirectories))
                {
                    FilePathInfo filepath = GetFilePathInfo(txtSourceFolder.Text, filename);

                    switch (System.IO.Path.GetExtension(filename).ToLower())
                    {
                        case ".swift":
                            var results = Parser_Swift.Parse(filepath);
                            results = WriteResults_ToDB(results, dal);
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


                foreach (string filename in Directory.EnumerateFiles(txtSourceFolder.Text, "*", SearchOption.AllDirectories))
                {
                    FilePathInfo filepath = GetFilePathInfo(txtSourceFolder.Text, filename);

                    switch (System.IO.Path.GetExtension(filename).ToLower())
                    {
                        case ".swift":
                            var results = Parser_Swift.Parse(filepath);

                            CodeDescription[] descriptions = code_describer.Describe(results);

                            WriteResults_ToFile(output_folder_snippets, results);
                            //WriteResults_ToFile(output_folder_descriptions, descriptions);
                            break;
                    }
                }








                //// TODO: put this in its own class


                //var client = new OllamaApiClient(txtOllamaURL.Text, txtOllamaModel.Text);

                //var chatService = new OllamaChatCompletionService(txtOllamaModel.Text, client);


                //// is a new instance of chathistory needed for each oneshot call?
                //// can the last user message be removed (containg the prev snippet), then the next snippet added?


                //var chatHistory = new ChatHistory("You are an assistant designed to summarize code snippets. Your task is to describe each given function or class in a concise and detailed manner using programming terms and concepts. The description should be a maximum of three sentences, focusing on the purpose, input parameters, output, and any key features of the function. Keep your description clear, precise, and relevant to its functionality. The output will be directly used for embedding, so it's important to use language that is likely to appear in related questions about the code.");


                //// foreach snippet
                ////{

                //chatHistory.AddUserMessage("<snippet contents>");

                ////if(prev)
                ////chatHistory.RemoveAt

                //// prev = true

                //var reply = chatService.GetChatMessageContentAsync(chatHistory).Result;

                ////CodeDescription

                ////}



            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
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

        private static CodeFile WriteResults_ToDB(CodeFile results, DAL_SQLDB dal)
        {
            var new_snippets = new List<CodeSnippet>();

            foreach (CodeSnippet snippet in results.Snippets)
                new_snippets.Add(dal.AddSnippet(snippet, results.Folder, results.File));

            return results with { Snippets = new_snippets.ToArray() };
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

        #endregion
    }
}