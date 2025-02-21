using RAGSnippetBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static RAGSnippetBuilder.ParseCode.Parse_Swift_Line;

namespace RAGSnippetBuilder.ParseCode
{
    // TODO: look harder for existing parsers
    //  AST
    //  CodeQL

    public static class Parser_Swift
    {
        #region enum: CurrentType

        private enum CurrentType
        {
            Other,
            Class,
            Struct,
            Enum,
            Func,
        }

        #endregion
        #region class: BuildingSnippet

        private class BuildingSnippet
        {
            public CurrentType CurrentType = CurrentType.Other;

            public string NameSpace = "";
            public string ParentName = "";
            public string Inheritance = "";
            public string Name = "";

            /// <summary>
            /// If this is function, this will be the line that defines the function.  Class name would be some line above,
            /// but that index isn't important (just the name)
            /// </summary>
            public int StartIndex = 0;

            public List<string> Lines = new List<string>();
            public List<string> Lines_NoComment = new List<string>();
        }

        #endregion

        // When adding more languages, this outer function and currenttype enum can probably stay the same.  Just use
        // language specific private functions
        //
        // One exception is that in swift, enums can contain functions, need a helper bool

        /// <summary>
        /// Breaks the file into snippets of functions, enums, tops of classes
        /// </summary>
        /// <remarks>
        /// One failure of this is if properties and member variables are between functions or after enums, they will
        /// become part of the snippet above them instead of being part of the class's snippet
        /// </remarks>
        public static CodeFile Parse(FilePathInfo filepath, Func<long> get_next_id)
        {
            var retVal = new List<CodeSnippet>();

            // Code Block level vars
            var building = new BuildingSnippet()
            {
                NameSpace = filepath.Folder,
            };

            // Line level vars
            string line;
            int line_num = 0;

            SpanType state = SpanType.Other;
            BlockDelimiters delimiters = null;

            using (StreamReader reader = new StreamReader(filepath.FullFilename))
            {
                while ((line = reader.ReadLine()) != null)
                {
                    line_num++;

                    // Separate into code, strings, comments
                    var spans = Parse_Swift_Line.ParseLine(line, state, delimiters);

                    string line_nocomment = spans.spans.
                        Where(o => o.Type != SpanType.Comment).
                        Select(o => o.Text).
                        ToJoin(" ");

                    // Identify type, maybe flush buffer, add to buffer
                    building = ParseLine(retVal, building, line_num, line, line_nocomment, get_next_id);

                    state = spans.state;
                    delimiters = spans.delimiters;
                }

                MaybeFinishExisting(retVal, building, line_num + 1, get_next_id);
            }


            // TODO: post process to find all classes, then place the interface in them (function definitions, but no content)
            // Also look for properties that ended up with functions


            return CodeFile.BuildOuter(filepath) with
            {
                Snippets = retVal.ToArray(),
            };
        }

        #region Private Methods

        private static BuildingSnippet ParseLine(List<CodeSnippet> snippets, BuildingSnippet building, int line_num, string line, string line_nocomment, Func<long> get_next_id)
        {
            CurrentType new_type = IdentifyNewType(line_nocomment);

            switch (new_type)
            {
                case CurrentType.Other:
                    AddToExisting(building.Lines, building.Lines_NoComment, line, line_nocomment);
                    break;

                case CurrentType.Class:
                case CurrentType.Struct:
                    building = MaybeFinishExisting(snippets, building, line_num, get_next_id);

                    building.CurrentType = new_type;

                    var parent_name = GetClassStructName(line_nocomment);
                    building.ParentName = parent_name.name;
                    building.Inheritance = parent_name.inheritance;
                    building.Name = parent_name.name;

                    AddToExisting(building.Lines, building.Lines_NoComment, line, line_nocomment);
                    break;

                case CurrentType.Enum:
                    building = MaybeFinishExisting(snippets, building, line_num, get_next_id);

                    building.CurrentType = new_type;

                    building.Name = GetEnumFuncName(line_nocomment);

                    AddToExisting(building.Lines, building.Lines_NoComment, line, line_nocomment);
                    break;

                case CurrentType.Func:
                    if (building.CurrentType == CurrentType.Enum)
                    {
                        AddToExisting(building.Lines, building.Lines_NoComment, line, line_nocomment);
                    }
                    else
                    {
                        building = MaybeFinishExisting(snippets, building, line_num, get_next_id);

                        building.CurrentType = new_type;

                        building.Name = GetEnumFuncName(line_nocomment);

                        AddToExisting(building.Lines, building.Lines_NoComment, line, line_nocomment);
                    }
                    break;

                default:
                    throw new ApplicationException($"Unknown CurrentType: {new_type}");
            }

            return building;
        }

        private static void AddToExisting(List<string> lines, List<string> lines_nocomment, string line, string line_nocomment)
        {
            lines.Add(line);
            lines_nocomment.Add(line_nocomment);
        }

        // This gets called on lines that define a new class or struct.  If there is an existing function under
        // a different class, then flush that, but if it's the top of the file, then do nothing
        private static BuildingSnippet MaybeFinishExisting(List<CodeSnippet> snippets, BuildingSnippet building, int line_num, Func<long> get_next_id)
        {
            if (building.CurrentType == CurrentType.Other)
                return building;

            TrimBlankLines(building.Lines);
            TrimBlankLines(building.Lines_NoComment);

            if (building.Lines.Count > 0 || building.Lines_NoComment.Count > 0)
                snippets.Add(new CodeSnippet()
                {
                    UniqueID = get_next_id(),

                    LineFrom = building.StartIndex,
                    LineTo = line_num - 1,

                    NameSpace = building.NameSpace,

                    ParentName = building.CurrentType == CurrentType.Enum ?     // the odds are better that enums are standalone vs inside classes.  It's going to be wrong in half the scenarios (unless curly braces are tracked)
                        "" :
                        building.ParentName,

                    Inheritance = building.CurrentType == CurrentType.Enum ?
                        "" :
                        building.Inheritance,

                    Name = building.Name,

                    Type = ConvertSnippetType(building.CurrentType),

                    Text = string.Join('\n', building.Lines),
                    //Text_NoComments = string.Join('\n', building.Lines_NoComment),
                });

            return StartNewSnippet(building, line_num);
        }

        private static CodeSnippetType ConvertSnippetType(CurrentType type)
        {
            switch (type)
            {
                case CurrentType.Other:
                    throw new ApplicationException($"Can't convert type: {type}");

                case CurrentType.Class:
                case CurrentType.Struct:
                    return CodeSnippetType.Class_shell;

                case CurrentType.Enum:
                    return CodeSnippetType.Enum;

                case CurrentType.Func:
                    return CodeSnippetType.Func;

                default:
                    throw new ApplicationException($"Unknown CurrentType: {type}");
            }
        }

        private static BuildingSnippet StartNewSnippet(BuildingSnippet prev, int line_num)
        {
            // NOTE: CurrentType and Name will be populated by the caller

            return new BuildingSnippet()
            {
                NameSpace = prev.NameSpace,
                ParentName = prev.ParentName,
                Inheritance = prev.Inheritance,
                StartIndex = line_num,
            };
        }

        private static (string name, string inheritance) GetClassStructName(string line_nocomments)
        {
            Match name_match = Regex.Match(line_nocomments, @"(struct|class)\s+(?<name>\w+)");
            if (!name_match.Success)
                return ("", "");        // since this function is only called when struct or class with a space after is found, this must not have any name after

            string name = name_match.Groups["name"].Value;

            string remainder = line_nocomments.Substring(name_match.Index + name_match.Length);

            Match inherit_match = Regex.Match(remainder, @"(:| extends )");
            if (!inherit_match.Success)
                return (name, "");

            remainder = remainder.Substring(inherit_match.Index + inherit_match.Length);

            int curly_index = remainder.IndexOf('{');

            if (curly_index == 0)
                return (name, "");      // invalid syntax, but could happen

            if (curly_index > 0)
                remainder = remainder.Substring(0, curly_index);

            return (name, remainder.Trim());
        }
        private static string GetEnumFuncName(string line_nocomments)
        {
            Match match = Regex.Match(line_nocomments, @"(enum|func)\s+(?<name>\w+)");

            return match.Success ?
                match.Groups["name"].Value :
                "";
        }

        private static CurrentType IdentifyNewType(string line)
        {
            if (IdentifyNewType_HasText(line, "func"))
                return CurrentType.Func;

            else if (IdentifyNewType_HasText(line, "class"))
                return CurrentType.Class;

            else if (IdentifyNewType_HasText(line, "enum"))
                return CurrentType.Enum;

            else if (IdentifyNewType_HasText(line, "struct"))
                return CurrentType.Struct;

            else
                return CurrentType.Other;
        }
        private static bool IdentifyNewType_HasText(string line, string qual)
        {
            return
                line.StartsWith($"{qual} ") ||      // keeping it case sensitive
                line.Contains($" {qual} ");
        }

        private static string FolderToNamespace(string folder)
        {
            return FolderToFinal(folder).       // this one makes sure that \ is /
                Replace('/', '.');
        }
        private static string FolderToFinal(string folder)
        {
            return folder.Replace('\\', '/');
        }

        /// <summary>
        /// Removes blank lines from the beginning and ending of the list.  Doesn't remove blank lines inside the actual snippet
        /// </summary>
        private static void TrimBlankLines(List<string> lines)
        {
            // Remove initial blank lines
            while (lines.Count > 0)
                if (lines[0].Trim() == "")
                    lines.RemoveAt(0);
                else
                    break;

            // Remove trailing blank lines
            while (lines.Count > 0)
                if (lines[^1].Trim() == "")
                    lines.RemoveAt(lines.Count - 1);
                else
                    break;
        }

        #endregion
    }
}
