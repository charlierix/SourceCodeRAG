using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    /// <summary>
    /// This will hold an LLM's description of a code snippet
    /// </summary>
    /// <remarks>
    /// There will probably be multiple descriptions made of each snippet.  UniqueID is the link (foreign key) back
    /// to the actual code snippet
    /// </remarks>
    public record CodeDescription
    {
        // Matches unique ID in CodeSnippet
        public long UniqueID { get; init; }

        public string Description { get; init; }
    }
}
