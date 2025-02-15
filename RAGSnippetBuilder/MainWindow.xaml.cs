using Game.Core;
using Game.Core.Threads;
using Game.Math_WPF.WPF;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Embeddings;
using OllamaSharp;
using RAGSnippetBuilder.Chroma;
using RAGSnippetBuilder.DAL;
using RAGSnippetBuilder.LLM;
using RAGSnippetBuilder.Models;
using RAGSnippetBuilder.ParseCode;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Effects;

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

        #region record: ModelDetail

        public record ModelDetail
        {
            public string Name { get; init; }
            public string ParameterSize { get; init; }
            public string QuantizationLevel { get; init; }
            public string Family { get; init; }
            public string TotalSize { get; init; }
        }

        #endregion
        #region record: OllamaQuery

        private record OllamaQuery_Request
        {
            public string URL { get; init; }
        }

        private record OllamaQuery_Response
        {
            public OllamaSharp.Models.Model[] Models { get; init; }
            public Exception Ex { get; init; }
        }

        #endregion

        #region Declaration Section

        public ObservableCollection<string> ModelList { get; private set; } = [];
        public ObservableCollection<ModelDetail> ModelDetailsList { get; private set; } = [];

        /// <summary>
        /// This does work in a background thread and makes sure that only the last call to start
        /// calls finish event.  All intermediate calls to start are silently ignored
        /// 
        /// This allows the user to type out the url without issue
        /// </summary>
        private readonly BackgroundTaskWorker<OllamaQuery_Request, OllamaQuery_Response> _modelQuery;

        private readonly DropShadowEffect _errorEffect;

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();

            DataContext = this;

            Background = SystemColors.ControlBrush;

            _modelQuery = new BackgroundTaskWorker<OllamaQuery_Request, OllamaQuery_Response>(GetOllamaModels, FinishedOllamaModels, ExceptionOllamaModels);

            _errorEffect = new DropShadowEffect()
            {
                Color = UtilityWPF.ColorFromHex("C02020"),
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = 8,
                Opacity = .8,
            };
        }

        #endregion

        #region Event Listeners

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = SettingsManager.Settings;

                txtOllamaURL.Text = settings.llm.url;
                cboOllamaModelDescribe.Text = settings.llm.model_describe;
                txtOllamaThreadsDescribe.Text = settings.llm.max_threads_describe.ToString();
                cboOllamaModelEmbed.Text = settings.llm.model_embed;
                txtOllamaThreadsEmbed.Text = settings.llm.max_threads_embed.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtOllamaURL_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                var request = new OllamaQuery_Request()
                {
                    URL = txtOllamaURL.Text,
                };

                ModelList.Clear();
                ModelDetailsList.Clear();
                txtOllamaURL.Effect = _errorEffect;     // let the finish task set this to null if valid

                _modelQuery.Start(request);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private static OllamaQuery_Response GetOllamaModels(OllamaQuery_Request request, CancellationToken cancel)
        {
            try
            {
                if (cancel.IsCancellationRequested)
                    return null;        // it doesn't matter what gets returned, it will be ignored

                var client = new OllamaApiClient(request.URL);
                var models = client.ListLocalModelsAsync().
                    GetAwaiter().
                    GetResult().
                    ToArray();

                return new OllamaQuery_Response() { Models = models };
            }
            catch (Exception ex)
            {
                return new OllamaQuery_Response() { Ex = ex };
            }
        }
        private void FinishedOllamaModels(OllamaQuery_Request request, OllamaQuery_Response response)
        {
            try
            {
                // NOTE: this gets invoked on the main thread, so the below is threadsafe

                ModelList.Clear();
                ModelDetailsList.Clear();

                if (response.Ex != null)
                {
                    txtOllamaURL.Effect = _errorEffect;
                    return;
                }

                var models = response.Models.
                    OrderByDescending(o => o.Size).
                    ThenBy(o => o.Name).
                    ToArray();

                foreach (var model in models)
                {
                    ModelList.Add(model.Name);
                    ModelDetailsList.Add(new ModelDetail()
                    {
                        Name = model.Name,
                        ParameterSize = model.Details.ParameterSize,
                        QuantizationLevel = model.Details.QuantizationLevel,
                        Family = model.Details.Family,
                        TotalSize = UtilityCore.Format_SizeSuffix(model.Size),
                    });
                }

                txtOllamaURL.Effect = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ExceptionOllamaModels(OllamaQuery_Request request, Exception ex)
        {
            try
            {
                // NOTE: this gets invoked on the main thread, so this is threadsafe
                txtOllamaURL.Effect = _errorEffect;
            }
            catch (Exception ex1)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

                if (!int.TryParse(txtOllamaThreadsDescribe.Text, out int llm_describe_threads))
                {
                    MessageBox.Show("Couldn't parse ollama threads (describe) as an integer", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(txtOllamaThreadsDescribe.Text, out int llm_embed_threads))
                {
                    MessageBox.Show("Couldn't parse ollama threads (embed) as an integer", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create subfolders in the output
                string source_leaf_name = System.IO.Path.GetFileName(txtSourceFolder.Text);
                string output_folder = System.IO.Path.Combine(txtOutputFolder.Text, $"{source_leaf_name} {DateTime.Now:yyyyMMdd HHmmss}");
                string output_folder_db = System.IO.Path.Combine(output_folder, "db");
                string output_folder_json = System.IO.Path.Combine(output_folder, "json");
                string output_folder_snippets = System.IO.Path.Combine(output_folder_json, "snippets");
                string output_folder_descriptions = System.IO.Path.Combine(output_folder_json, "descriptions");
                string output_folder_questions = System.IO.Path.Combine(output_folder_json, "questions");
                string output_folder_tags = System.IO.Path.Combine(output_folder_json, "tags");

                Directory.CreateDirectory(output_folder);
                Directory.CreateDirectory(output_folder_db);
                Directory.CreateDirectory(output_folder_json);
                Directory.CreateDirectory(output_folder_snippets);
                Directory.CreateDirectory(output_folder_descriptions);
                Directory.CreateDirectory(output_folder_questions);
                Directory.CreateDirectory(output_folder_tags);



                // TODO: Make a readme file that describes this output folder


                // TODO: manifest.json



                // Make sure the table is empty
                //new DAL_SQLDB(output_folder_db).TruncateTables();     - no need, the folder is newly created

                // Use this so the main thread doesn't get held up as bad (hopefully allowing llm writer to get more done)
                var dal = new DALTaskWrapper(output_folder_db);

                // LLM caller
                var code_describer = new LLM_Describe(txtOllamaURL.Text, cboOllamaModelDescribe.Text, llm_describe_threads);
                var embedder = new LLM_Embed(txtOllamaURL.Text, cboOllamaModelEmbed.Text, llm_embed_threads);

                // Chroma
                var chroma = new ChromaWrapper(@"D:\!dev_repos\SourceCodeRAG\ChromaTest2", output_folder_db);

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

                            var embedding_results = embedder.Embed(results, llm_results);

                            foreach (CodeSnippet snippet in results.Snippets)
                                dal.Add(snippet, results.Folder, results.File);

                            foreach (var tags in llm_results.Select(o => o.Tags))
                                dal.Add(tags);

                            chroma.Add(embedding_results).Wait();

                            WriteResults_ToFile(output_folder_snippets, results);
                            WriteResults_ToFile(output_folder_descriptions, results.File, llm_results.Select(o => o.Description).ToArray());
                            WriteResults_ToFile(output_folder_questions, results.File, llm_results.Select(o => o.Questions).ToArray());
                            WriteResults_ToFile(output_folder_tags, results.File, llm_results.Select(o => o.Tags).ToArray());
                            break;
                    }
                }

                dal.Finished();

                SaveSettings();

                MessageBox.Show("Finished", Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UnitTests_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SettingsModel settings = ScrapeSettings();
                if (settings == null)
                    return;

                SaveSettings(settings);

                new UnitTestsWindow(settings, txtOutputFolder.Text).
                    Show();
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Private Methods

        private void SaveSettings(SettingsModel settings = null)
        {
            settings = settings ?? ScrapeSettings();
            if (settings == null)
                return;

            SettingsManager.Save(settings);
        }

        private SettingsModel ScrapeSettings()
        {
            if (!int.TryParse(txtOllamaThreadsDescribe.Text, out int max_threads_describe))
            {
                MessageBox.Show("Couldn't parse max threads (describe)", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            if (!int.TryParse(txtOllamaThreadsEmbed.Text, out int max_threads_embed))
            {
                MessageBox.Show("Couldn't parse max threads", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                return null;
            }

            var settings = SettingsManager.Settings;

            return settings with
            {
                llm = settings.llm with
                {
                    url = txtOllamaURL.Text,
                    model_describe = cboOllamaModelDescribe.Text,
                    max_threads_describe = max_threads_describe,
                    model_embed = cboOllamaModelEmbed.Text,
                    max_threads_embed = max_threads_embed,
                }
            };
        }

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

        private static async Task<string> Process_LLM(IChatCompletionService chatService, ChatHistory chat, int system_count, string input)
        {
            // Get rid of previous call
            while (chat.Count > system_count)
                chat.RemoveAt(chat.Count - 1);

            chat.AddUserMessage(input);

            var reply = await chatService.GetChatMessageContentAsync(chat);

            var replies = reply.Items.      // when testing, .Items was count of one.  Each item under it was a (token?) word or partial word
                Select(o => o.ToString());      // ToString looks like it joins all the subwords together properly into the full response

            return string.Join(Environment.NewLine + Environment.NewLine, replies);     // there was only one, but if there are multiple, a couple newlines seems like a good delimiter
        }

        #endregion
    }
}