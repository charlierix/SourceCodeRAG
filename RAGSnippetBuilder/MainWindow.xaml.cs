using RAGSnippetBuilder.Models;
using System.IO;
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