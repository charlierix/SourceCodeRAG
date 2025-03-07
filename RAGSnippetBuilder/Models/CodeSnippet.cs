﻿using System;
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

        // Foreign key to parent
        public long? ParentID { get; init; }

        // Line numbers within the file that the text came from (zero based)
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

        // NOTE: if type is error, this will just be the raw text of the file
        public CodeSnippetType Type { get; init; }

        // If this is a Class_shell, then text will be the interface (what you would see in a .h file)
        // If this is a Class_full, it will be the entire text of the original class
        // If this is a function, then it will be the function and interior code
        public string Text { get; init; }
        //public string Text_NoComments { get; init; }      // there shouldn't be a need to expose Text_NoComments

        public override string ToString()
        {
            string text = "";
            if (!string.IsNullOrWhiteSpace(Name))
                text = Name;
            else if(!string.IsNullOrWhiteSpace(Text))
                text = Text;

            return $"{Type}: {text}";
        }
    }

    public enum CodeSnippetType
    {
        Class_shell,
        Class_full,
        Enum,
        Func,
        Error,      // the snippet will be filled out minimally, but still contain the text
    }
}
