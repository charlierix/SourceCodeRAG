using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    public record FilePathInfo
    {
        /// <summary>
        /// The root foldername
        /// </summary>
        public string SourceFolder { get; init; }
        /// <summary>
        /// The full path and filename
        /// </summary>
        public string FullFilename { get; init; }

        /// <summary>
        /// The folder names under SourceFolder (doesn't include any of SourceFolder)
        /// </summary>
        public string Folder { get; init; }
        /// <summary>
        /// Just the filename, no folder
        /// </summary>
        public string File { get; init; }
    }
}
