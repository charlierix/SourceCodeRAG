using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.Models
{
    public record CodeSnippet
    {
        // This is a unique ID across all code snippets and can be used to generate id for rag collections
        public long UniqueID { get; init; }

        // Line numbers within the file that the text came from
        public int LineFrom { get; init; }
        public int LineTo { get; init; }

        // Defaults to folder structure for languages that don't support namespaces
        public string NameSpace { get; init; }

        // If the type is func, then this would be the name of the class its in
        public string ParentName { get; init; }
        // List of base classes and interfaces that class or parent class inherits/implements
        public string Inheritance { get; init; }

        // This is the name of the item that the snippet represents
        public string Name { get; init; }

        // This could be an enum instead of string, but this class will be serialized to json, so just making it string
        // class, struct, enum, func
        public string Type { get; init; }

        public string Text { get; init; }
        // NOTE: if // or /* */ are inside of a string's double quotes, they will be seen as comments
        // So "url=http://this/here" will cut off after http:
        public string Text_NoComments { get; init; }
    }
}
