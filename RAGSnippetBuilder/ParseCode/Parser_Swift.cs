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

            // Find all classes and rebuild to look like interfaces (fields/props, function definitions)
            // Also generate full version copies of the classes
            RebuildClasses(retVal);


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

        private static void RebuildClasses(List<CodeSnippet> snippets)
        {
            var full_classes = new List<CodeSnippet>();



            int[] class_indices = RebuildClasses_GetClassIndices(snippets);





            //for (int i = 0; i < snippets.Count; i++)
            //{
            //    if (snippets[i].Type != CodeSnippetType.Class_shell)
            //        continue;

            //    var children = snippets.
            //        Where(o => o.ParentID == snippets[i].UniqueID).
            //        ToArray();




            //}

            snippets.AddRange(full_classes);
        }

        /// <summary>
        /// Returns indices of Class_shell, and makes sure that any child classes are at the front of the list so
        /// that they are rebuilt before getting to their parent (so the parent can simply append child class's text
        /// to itself)
        /// </summary>
        private static int[] RebuildClasses_GetClassIndices(IList<CodeSnippet> snippets)
        {
            int[] class_indices = Enumerable.Range(0, snippets.Count).
                Where(o => snippets[o].Type == CodeSnippetType.Class_shell).
                ToArray();

            if (class_indices.Length < 2)
                return class_indices;

            // Create a mapping from UniqueID to index
            var trees = RebuildClasses_GetClassIndices_Tree(snippets, class_indices);

            // Perform post-order traversal to collect indices
            return RebuildClasses_GetClassIndices_ChildTraverse(trees.roots, trees.parentChildren);
        }
        /// <summary>
        /// Creates a tree structure for the given class indices, establishing parent-child relationships based on UniqueIDs
        /// </summary>
        /// <param name="class_indices">Indices in the snippets list that are Class_shell</param>
        /// <returns>A tuple containing:
        /// - An array of root indices (those without a parent)
        /// - A dictionary mapping each parent index to its list of child indices
        ///</returns>
        private static (int[] roots, Dictionary<int, List<int>> parentChildren) RebuildClasses_GetClassIndices_Tree(IList<CodeSnippet> snippets, int[] class_indices)
        {
            var idToIndex = new Dictionary<long, int>();
            for (int i = 0; i < class_indices.Length; i++)
                idToIndex.Add(snippets[class_indices[i]].UniqueID, class_indices[i]);

            // Build the children hierarchy
            var parentChildren = new Dictionary<int, List<int>>();
            var roots = new List<int>();

            for (int i = 0; i < class_indices.Length; i++)
            {
                if (snippets[class_indices[i]].ParentID == null)
                {
                    roots.Add(class_indices[i]);
                }
                else
                {
                    long parentId = snippets[class_indices[i]].ParentID.Value;
                    int parentIndex = idToIndex[parentId];
                    if (!parentChildren.ContainsKey(parentIndex))
                        parentChildren[parentIndex] = new List<int>();
                    parentChildren[parentIndex].Add(class_indices[i]);
                }
            }

            return (roots.ToArray(), parentChildren);
        }
        /// <summary>
        /// Performs a post-order traversal of the class tree to collect indices in an order where child classes precede their parents
        /// </summary>
        /// <param name="roots">Indices of root nodes (classes without parents) in the tree</param>
        /// <param name="parentChildren">Dictionary mapping each parent index to its list of child indices</param>
        /// <returns>An array of class indices ordered such that all children appear before their parent classes</returns>
        private static int[] RebuildClasses_GetClassIndices_ChildTraverse(int[] roots, Dictionary<int, List<int>> parentChildren)
        {
            // Keeps track of nodes to visit.  processed indicates whether this node has been added to the result list
            var stack = new Stack<(int index, bool processed)>();

            // This list will store the final order of indices where children come before their parents
            var sortedIndices = new List<int>();

            // Initialize the stack with all root nodes.  They start as unprocessed (false), meaning we need to visit
            // them and their children first
            foreach (int root in roots)
                stack.Push((root, false));

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (current.processed)
                {
                    // The node is marked as processed, which means all of its descendants have already been added to
                    // 'sortedIndices'.  Can now add this parent node to the result list
                    sortedIndices.Add(current.index);
                }
                else
                {
                    // The node is not processed, add back as processed (true).  This ensures when encountered again,
                    // it can be added to the result list (because its children will be pushed in front of it)
                    stack.Push((current.index, true));

                    // Push all children of the current node onto the stack with 'processed' set to false.  Since a stack
                    // is used (LIFO), these children will be processed before their parent when they are popped from the
                    // stack in subsequent iterations
                    if (parentChildren.ContainsKey(current.index))
                        foreach (int child in parentChildren[current.index])
                            stack.Push((child, false));
                }
            }

            return sortedIndices.ToArray();
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
