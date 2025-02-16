using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    public record CodeFile
    {
        /// <summary>
        /// The folder names under the root folder of the repo
        /// </summary>
        /// <remarks>
        /// Using forward slash so that it's consistent across os
        /// 
        /// 
        /// If the repo is in:
        /// C:\dev\reponame
        /// 
        /// And this class is:
        /// C:\dev\reponame\class.cs
        /// 
        /// Then Folder will be ""
        /// 
        /// But if this class is:
        /// C:\dev\reponame\subfolder\extra\class.cs
        /// 
        /// This Folder will be:
        /// "subfolder/extra"
        /// </remarks>
        public string Folder { get; init; }

        /// <summary>
        /// Just the filename, no folder
        /// </summary>
        public string File { get; init; }

        /// <summary>
        /// The file broken up into enums, functions, lines between them
        /// </summary>
        public CodeSnippet[] Snippets { get; init; }

        /// <summary>
        /// Returns a new instance of CodeFile with just the file props filled out (snippets is left null)
        /// </summary>
        public static CodeFile BuildOuter(FilePathInfo filepath)
        {
            return new CodeFile()
            {
                Folder = filepath?.Folder?.Replace('\\', '/'),
                File = filepath?.File,
            };
        }
    }
}
