//using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.Solution;
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.IO.Pipes;
using System.Printing;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Windows;

namespace RAGSnippetBuilder.Decompile
{
    // https://github.com/icsharpcode/ILSpy/tree/master
    public partial class DecompileWindow : Window
    {
        public DecompileWindow()
        {
            InitializeComponent();

            Background = SystemColors.ControlBrush;
        }

        private void Decompile1_Click(object sender, RoutedEventArgs e)
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

                string source_folder = txtSourceFolder.Text;
                string output_folder = txtOutputFolder.Text;


                var decompile_settings = new ICSharpCode.Decompiler.DecompilerSettings()
                {
                    ThrowOnAssemblyResolveErrors = false,
                };

                foreach (string filename in Directory.EnumerateFiles(source_folder, "*.dll", SearchOption.AllDirectories))
                {
                    // Create an output folder based on the dll name
                    string project_folder = GetOutputProjectFolder(filename, source_folder, output_folder);

                    Directory.CreateDirectory(project_folder);


                    var decompiler = new ICSharpCode.Decompiler.CSharp.CSharpDecompiler(filename, decompile_settings);

                    var result = decompiler.Decompile();



                    // https://github.com/icsharpcode/ILSpy/tree/master



                    //                    if (result.Project.Files.Count > 0)
                    //                    {
                    //                        foreach (var file in result.Project.Files)
                    //                        {
                    //                            string filePath = Path.Combine(project_folder, file.FileName);
                    //                            File.WriteAllText(filePath, file.Content);
                    //                        }

                    //                        // Create a simple .csproj file
                    //                        string projectFileContent = $@"
                    //<Project Sdk=""Microsoft.NET.Sdk"">

                    //  <PropertyGroup>
                    //    <OutputType>WinExe</OutputType>
                    //    <TargetFramework>net6.0</TargetFramework>
                    //    <Nullable>enable</Nullable>
                    //    <ImplicitUsings>enable</ImplicitUsings>
                    //  </PropertyGroup>

                    //</Project>
                    //                        ";

                    //                        string csprojPath = Path.Combine(project_folder, $"{project_}.csproj");
                    //                        File.WriteAllText(csprojPath, projectFileContent);
                    //                    }




                }

                //string inputDir = @"C:\path\to\dlls";
                //string outputDir = @"C:\path\to\output";

                //foreach (string dllPath in Directory.GetFiles(inputDir, "*.dll"))
                //{
                //    string dllName = Path.GetFileNameWithoutExtension(dllPath);
                //    string outputFolder = Path.Combine(outputDir, dllName);

                //    // Create output folder if it doesn't exist
                //    if (!Directory.Exists(outputFolder))
                //        Directory.CreateDirectory(outputFolder);

                //    // Decompile the DLL and save to the output folder
                //    var decompiler = new Decompiler();
                //    decompiler.Decompile(dllPath);

                //    // Save each file in the output folder
                //    foreach (var file in decompiler.Project.Files)
                //    {
                //        string filePath = Path.Combine(outputFolder, file.FileName);
                //        File.WriteAllText(filePath, file.Content);
                //    }
                //}

                //Console.WriteLine("Decompilation completed successfully.");




            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Decompile2_Click(object sender, RoutedEventArgs e)
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

                string source_folder = txtSourceFolder.Text;
                string output_folder = txtOutputFolder.Text;

                var decompile_settings = new ICSharpCode.Decompiler.DecompilerSettings()
                {
                    ThrowOnAssemblyResolveErrors = false,
                };


                // TODO: scan for all dlls up front, group by containing folder
                // only create dllname_dll subfolder if there are multiple dlls in the same folder


                foreach (string filename in Directory.EnumerateFiles(source_folder, "*.dll", SearchOption.AllDirectories))
                {
                    string dll_sourcefolder = System.IO.Path.GetDirectoryName(filename);

                    // Create an output folder based on the dll name
                    string project_folder = GetOutputProjectFolder(filename, source_folder, output_folder);

                    Directory.CreateDirectory(project_folder);


                    using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                    {
                        // TODO: target framework defaults to 4.8 if null passed in.  My want to create a 4.8 output folder and a netcore output folder, decompile twice, keep the one with fewer errors
                        //var resolver = new UniversalAssemblyResolver(filename, false, null);

                        PEFile module = new PEFile(filename, fileStream, PEStreamOptions.PrefetchEntireImage);
                        var resolver = new UniversalAssemblyResolver(filename, false, module.Metadata.DetectTargetFrameworkId(), null, PEStreamOptions.PrefetchMetadata, MetadataReaderOptions.ApplyWindowsRuntimeProjections);

                        //var assemblyNames = new DirectoryInfo(dll_sourcefolder).EnumerateFiles("*.dll").Select(f => Path.GetFileNameWithoutExtension(f.Name));
                        //foreach (var name in assemblyNames)
                        //    localAssemblies.Add(name);


                        //var resolver = new ICSharpCode.Decompiler.Tests.TestAssemblyResolver();       // private


                        resolver.AddSearchDirectory(dll_sourcefolder);
                        resolver.RemoveSearchDirectory(".");


                        //ProjectFileWriterSdkStyle
                        //ProjectFileWriterDefault

                        //var project_writer = new ICSharpCode.Decompiler.CSharp.ProjectDecompiler.ProjectFileWriterDefault();      // for some reason, they made this class private


                        var decompiler = new ICSharpCode.Decompiler.CSharp.ProjectDecompiler.WholeProjectDecompiler(decompile_settings, resolver, null, resolver, null);
                        decompiler.DecompileProject(module, project_folder);

                    }

                }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), Title, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GetOutputProjectFolder(string dll_filename, string source_folder, string output_folder)
        {
            // Get the subfolders source_folder that the dll file sits in
            string input_folder = System.IO.Path.GetDirectoryName(dll_filename);

            // Add the dll's name, but with an underscore
            string folder_diff = System.IO.Path.GetRelativePath(source_folder, dll_filename);
            folder_diff = Regex.Replace(folder_diff, @"\.dll$", "_dll", RegexOptions.IgnoreCase);

            string retVal = System.IO.Path.Combine(output_folder, folder_diff);

            // Throw a guid at the end if that folder already exists (should never happen)
            if (Directory.Exists(retVal))
                retVal += "_" + Guid.NewGuid().ToString();

            return retVal;
        }
    }
}
