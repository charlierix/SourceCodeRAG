using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RAGSnippetBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;
using System.Xml.Linq;
using System.Xml;

namespace RAGSnippetBuilder.ParseCode
{
    public static class Parser_CSharp
    {
        #region record: ExtraClassInfo

        private record ExtraClassInfo
        {
            public string Comments { get; init; }
            public string Constraints { get; init; }
            public string Modifiers { get; init; }
            public string Attributes { get; init; }
            public string Type { get; init; }
            public string TypeParams { get; init; }        // this would be the "<T>" in "public class Something<T> {}"
        }

        #endregion
        #region record: ExtraFunctionInfo

        private record ExtraFunctionInfo
        {
            public string Comments { get; init; }
            public string Modifiers { get; init; }      // Space delimited list, like { public, private, static }
            public string Attributes { get; init; }     // "[attr]"
            public string Constraints { get; init; }        // "where TVertex : IVertex"
            public string ReturnType { get; init; }
            public string TypeParams { get; init; }        // this would be the "<T>" in "private static T GetIt<T>(T something) {}"
            public string ParamList { get; init; }
        }

        #endregion

        public static CodeFile Parse(FilePathInfo filepath, Func<long> get_next_id)
        {
            string file_contents = File.ReadAllText(filepath.FullFilename);

            var syntaxTree = CSharpSyntaxTree.ParseText(file_contents);

            CodeSnippet[] snippets = null;
            var props = new Dictionary<long, List<CodeSnippet>>();       // these are properties, fields, etc (not enums or functions) by parent class ID.  They will get added to the class at the end
            var classes = new Dictionary<long, ExtraClassInfo>();
            var funcs = new Dictionary<long, ExtraFunctionInfo>();

            if (syntaxTree.TryGetRoot(out SyntaxNode root))
                snippets = ParseNode(syntaxTree.GetRoot(), null, null, get_next_id, props, classes, funcs);
            else
                snippets = [TextToSnippet(file_contents, get_next_id())];

            // Find all classes and rebuild to look like interfaces (fields/props, function definitions)
            RebuildClasses(snippets, props, classes, funcs);

            // Make sure every line ends in \n (they mostly should, but there might be some \r\n's still in there)
            for (int i = 0; i < snippets.Length; i++)
                if (snippets[i].Text != null)
                    snippets[i] = snippets[i] with
                    {
                        Text = snippets[i].Text.Replace("\r\n", "\n"),
                    };

            return CodeFile.BuildOuter(filepath) with
            {
                Snippets = snippets,
            };
        }

        #region Private Methods

        private static CodeSnippet[] ParseNode(SyntaxNode node, CodeSnippet parent, string ns_text, Func<long> get_next_id, Dictionary<long, List<CodeSnippet>> props, Dictionary<long, ExtraClassInfo> classes, Dictionary<long, ExtraFunctionInfo> funcs)
        {
            // the base class seems to be CompilationUnitSyntax, which has a .Members property
            // then the children appear to be any of these types (.Members is list of MemberDeclarationSyntax):

            //MemberDeclarationSyntax
            //    BaseNamespaceDeclarationSyntax                .Members
            //        NamespaceDeclarationSyntax
            //        FileScopedNamespaceDeclarationSyntax
            //    BaseTypeDeclarationSyntax
            //        TypeDeclarationSyntax                     .Members
            //            ClassDeclarationSyntax
            //            StructDeclarationSyntax
            //            InterfaceDeclarationSyntax
            //            RecordDeclarationSyntax
            //        EnumDeclarationSyntax
            //    DelegateDeclarationSyntax
            //    EnumMemberDeclarationSyntax
            //    BaseFieldDeclarationSyntax
            //        FieldDeclarationSyntax
            //        EventFieldDeclarationSyntax
            //    BaseMethodDeclarationSyntax
            //        MethodDeclarationSyntax
            //        OperatorDeclarationSyntax
            //        ConversionOperatorDeclarationSyntax
            //        ConstructorDeclarationSyntax
            //        DestructorDeclarationSyntax
            //    BasePropertyDeclarationSyntax
            //        PropertyDeclarationSyntax
            //        EventDeclarationSyntax
            //        IndexerDeclarationSyntax
            //    IncompleteMemberSyntax

            var retVal = new List<CodeSnippet>();

            var text = node.GetText();     // this is too much, it's everything at this node and below

            if (node is CompilationUnitSyntax comp)
            {
                // there doesn't appear to be anything of value in this comp (maybe usings?)
                foreach (var comp_member in comp.Members)
                    retVal.AddRange(ParseNode(comp_member, parent, ns_text, get_next_id, props, classes, funcs));
            }
            else if (node is BaseNamespaceDeclarationSyntax ns)
            {
                ns_text = string.Join(".", GetNamespaceParts(ns.Name));

                foreach (var ns_member in ns.Members)
                    retVal.AddRange(ParseNode(ns_member, parent, ns_text, get_next_id, props, classes, funcs));
            }
            else if (node is TypeDeclarationSyntax type)
            {
                var snippet = BuildSnippet_Class(type, parent, ns_text, get_next_id, classes);
                retVal.Add(snippet.full);
                retVal.Add(snippet.shell);

                foreach (var type_member in type.Members)
                    retVal.AddRange(ParseNode(type_member, snippet.shell, ns_text, get_next_id, props, classes, funcs));
            }
            else if (node is GlobalStatementSyntax glob)
            {
                retVal.Add(BuildSnippet_Class_NewStyle(glob, parent, ns_text, get_next_id));        // this is the .net core version of program.main that doesn't have standard class syntax
            }
            else if (node is EnumDeclarationSyntax enm)
            {
                retVal.Add(BuildSnippet_Enum(enm, parent, ns_text, get_next_id));
            }
            else if (node is BaseMethodDeclarationSyntax method)
            {
                retVal.Add(BuildSnippet_Function(method, parent, ns_text, get_next_id, funcs));
            }
            else if (node is BaseFieldDeclarationSyntax field)
            {
                BuildSnippet_Field_Prop(field, field.GetText().ToString(), parent, ns_text, get_next_id, props);
            }
            else if (node is BasePropertyDeclarationSyntax prop)
            {
                BuildSnippet_Field_Prop(prop, prop.GetText().ToString(), parent, ns_text, get_next_id, props);
            }
            else if (node is DelegateDeclarationSyntax del)
            {
                if(parent == null)
                    retVal.Add(BuildSnippet_Class_StandaloneDelegate(del, parent, ns_text, get_next_id));
                else
                    BuildSnippet_Field_Prop(del, del.GetText().ToString(), parent, ns_text, get_next_id, props);
            }
            else if (node is IncompleteMemberSyntax incomplete)
            {
                retVal.Add(BuildSnippet_Error(node, parent, ns_text, get_next_id));
            }
            else
            {
                retVal.Add(BuildSnippet_Error(node, parent, ns_text, get_next_id));
            }

            return retVal.ToArray();
        }

        private static (CodeSnippet shell, CodeSnippet full) BuildSnippet_Class(TypeDeclarationSyntax node, CodeSnippet parent, string ns_text, Func<long> get_next_id, Dictionary<long, ExtraClassInfo> classes)
        {
            // Extra Info
            string type = null;

            if (node is ClassDeclarationSyntax)
                type = "class";
            else if (node is StructDeclarationSyntax)
                type = "struct";
            else if (node is InterfaceDeclarationSyntax)
                type = "interface";
            else if (node is RecordDeclarationSyntax)
                type = "record";

            var extra = new ExtraClassInfo()
            {
                Attributes = RemoveAllLeadingIndents(node.AttributeLists.ToString()),       // RemoveAllLeadingIndents is needed for multi lines of attributes.  ToString returns something like "[attr1]\r\n    [attr2]\r\n    [attr3]"
                Constraints = node.ConstraintClauses.ToString(),
                Modifiers = node.Modifiers.ToString(),

                //node.ParameterList        // why do class have params?  is this for generics?
                //node.TypeParameterList

                Type = type,

                TypeParams = node.TypeParameterList?.ToString(),

                Comments = node.HasLeadingTrivia ?
                    GetComments(node.GetLeadingTrivia()) :
                    null,
            };

            // Shell (will look sort of like an interface)
            var shell = BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Class_shell,

                Name = node.Identifier.Text,
                Inheritance = node.BaseList?.ToString(),

                // Text will be filled out in a post scan

            };

            // Full (all text in one place)
            classes.Add(shell.UniqueID, extra);

            var full = BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Class_full,

                Name = node.Identifier.Text,
                Inheritance = node.BaseList?.ToString(),

                Text = node.GetText().ToString(),       // some classes could get pretty big, but there may be times when full class is better than a bunch of pieces
            };

            return (shell, full);
        }
        private static CodeSnippet BuildSnippet_Class_NewStyle(GlobalStatementSyntax node, CodeSnippet parent, string ns_text, Func<long> get_next_id)
        {
            return BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Class_full,

                Text = node.GetText().ToString(),       // this is the new style program.cs, so dump the whole thing directly into text without any cleanup
            };
        }
        private static CodeSnippet BuildSnippet_Class_StandaloneDelegate(DelegateDeclarationSyntax node, CodeSnippet parent, string ns_text, Func<long> get_next_id)
        {
            // Saw this statement sitting outside of a class.  Treat it like a class:
            // public delegate void AutoScrollHandler(object sender, AutoScrollArgs e);

            return BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Class_full,

                Text = node.GetText().ToString(),
            };
        }
        private static CodeSnippet BuildSnippet_Enum(EnumDeclarationSyntax node, CodeSnippet parent, string ns_text, Func<long> get_next_id)
        {
            return BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Enum,

                Name = node.Identifier.Text,

                Text = CleanupSnippetText(node.GetText().ToString()),
            };
        }
        private static CodeSnippet BuildSnippet_Function(BaseMethodDeclarationSyntax node, CodeSnippet parent, string ns_text, Func<long> get_next_id, Dictionary<long, ExtraFunctionInfo> funcs)
        {
            var extra = new ExtraFunctionInfo()
            {
                Attributes = RemoveAllLeadingIndents(node.AttributeLists.ToString()),       // RemoveAllLeadingIndents is needed for multi lines of attributes.  ToString returns something like "[attr1]\r\n    [attr2]\r\n    [attr3]"
                Modifiers = node.Modifiers.ToString(),
                ParamList = node.ParameterList.ToString(),
                Comments = node.HasLeadingTrivia ?
                    GetComments(node.GetLeadingTrivia()) :
                    null,
            };

            string name = null;

            if (node is MethodDeclarationSyntax method)
            {
                name = method.Identifier.Text;

                extra = extra with
                {
                    TypeParams = method.TypeParameterList?.ToString(),
                    Constraints = method.ConstraintClauses.ToString(),
                    ReturnType = method.ReturnType.ToString(),
                };
            }
            else if (node is ConstructorDeclarationSyntax construct)
            {
                name = construct.Identifier.Text;
            }
            else if (node is DestructorDeclarationSyntax destruct)
            {
                name = destruct.Identifier.Text;
            }

            var retVal = BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Func,
                Name = name,
                Text = CleanupSnippetText(node.GetText().ToString()),
            };

            funcs.Add(retVal.UniqueID, extra);

            return retVal;
        }
        private static CodeSnippet BuildSnippet_Error(SyntaxNode node, CodeSnippet parent, string ns_text, Func<long> get_next_id)
        {
            return BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Error,

                Text = node.GetText().ToString(),       // don't clean up
            };
        }

        private static void BuildSnippet_Field_Prop(SyntaxNode node, string text, CodeSnippet parent, string ns_text, Func<long> get_next_id, Dictionary<long, List<CodeSnippet>> props)
        {
            if (parent == null)
                throw new ApplicationException("Found a field/property without a parent");

            CodeSnippet snippet = BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Class_shell,       // there is no enum for fields/props, but they will end up in the parent class

                Text = CleanupSnippetText(text),
            };

            if (!props.ContainsKey(parent.UniqueID))
                props.Add(parent.UniqueID, new List<CodeSnippet>());

            props[parent.UniqueID].Add(snippet);
        }

        private static CodeSnippet BuildSnippet_Common(SyntaxNode node, CodeSnippet parent, string ns_text, Func<long> get_next_id)
        {
            return new CodeSnippet()
            {
                UniqueID = get_next_id(),
                ParentID = parent?.UniqueID,

                ParentName = parent?.Name,

                LineFrom = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                LineTo = node.GetLocation().GetLineSpan().EndLinePosition.Line,

                NameSpace = ns_text,
            };
        }

        private static void RebuildClasses(CodeSnippet[] snippets, Dictionary<long, List<CodeSnippet>> props, Dictionary<long, ExtraClassInfo> classes, Dictionary<long, ExtraFunctionInfo> funcs)
        {
            const int INDENT = 4;

            for (int i = 0; i < snippets.Length; i++)
            {
                if (snippets[i].Type != CodeSnippetType.Class_shell)
                    continue;

                var lines = new List<string>();

                lines.Add(RebuildClasses_ClassDefinition(snippets[i], classes));
                lines.Add("{");

                bool wrote_line = false;

                // Add all props
                if (props.TryGetValue(snippets[i].UniqueID, out List<CodeSnippet> class_props))
                {
                    foreach (var prop in class_props)
                    {
                        if (wrote_line)
                            lines.Add("");

                        wrote_line = true;

                        lines.Add(AddLeftChars(prop.Text, INDENT));
                    }
                }

                // Add function headers
                foreach (CodeSnippet snippet_func in snippets.Where(o => o.Type == CodeSnippetType.Func && o.ParentID == snippets[i].UniqueID))
                {
                    if (wrote_line)
                        lines.Add("");

                    wrote_line = true;

                    lines.Add(AddLeftChars(RebuildClasses_FunctionDefinition(snippet_func, funcs), INDENT));
                }

                lines.Add("}");

                snippets[i] = snippets[i] with
                {
                    Text = string.Join('\n', lines),
                };
            }
        }
        private static string RebuildClasses_ClassDefinition(CodeSnippet snippet, Dictionary<long, ExtraClassInfo> classes)
        {
            var retVal = new List<string>();

            string modifiers = null;
            string constraints = null;
            string type = null;
            string typeParams = null;
            if (classes.TryGetValue(snippet.UniqueID, out ExtraClassInfo extra))
            {
                modifiers = extra.Modifiers;
                constraints = extra.Constraints;
                type = extra.Type;
                typeParams = extra.TypeParams;

                if (!string.IsNullOrWhiteSpace(extra.Comments))
                    retVal.Add(extra.Comments);

                if (!string.IsNullOrWhiteSpace(extra.Attributes))
                    retVal.Add(extra.Attributes);
            }

            var class_line = new List<string>();

            if (!string.IsNullOrWhiteSpace(modifiers))
                class_line.Add(modifiers);

            if (!string.IsNullOrWhiteSpace(type))
                class_line.Add(type);

            class_line.Add(snippet.Name);

            if (!string.IsNullOrWhiteSpace(typeParams))
                class_line[^1] += typeParams;       // this is <T> so should have no whitespace between it and class name

            if (!string.IsNullOrWhiteSpace(snippet.Inheritance))
                class_line.Add(snippet.Inheritance);

            if (!string.IsNullOrWhiteSpace(constraints))
                class_line.Add(constraints);

            retVal.Add(string.Join(' ', class_line));

            return string.Join('\n', retVal);
        }
        private static string RebuildClasses_FunctionDefinition(CodeSnippet snippet, Dictionary<long, ExtraFunctionInfo> funcs)
        {
            var retVal = new List<string>();

            string modifiers = null;
            string returnType = null;
            string typeParams = null;
            string paramList = null;
            string constraints = null;
            if (funcs.TryGetValue(snippet.UniqueID, out ExtraFunctionInfo extra))
            {
                modifiers = extra.Modifiers;
                returnType = extra.ReturnType;
                typeParams = extra.TypeParams;
                paramList = extra.ParamList;
                constraints = extra.Constraints;

                if (!string.IsNullOrWhiteSpace(extra.Comments))
                    retVal.Add(extra.Comments);

                if (!string.IsNullOrWhiteSpace(extra.Attributes))
                    retVal.Add(extra.Attributes);
            }

            var func_line = new List<string>();

            if (!string.IsNullOrWhiteSpace(modifiers))
                func_line.Add(modifiers);

            if (!string.IsNullOrWhiteSpace(returnType))
                func_line.Add(returnType);

            func_line.Add(snippet.Name);

            if (!string.IsNullOrWhiteSpace(typeParams))
                func_line[^1] += typeParams;        // don't want a space between function name and <T>

            if (!string.IsNullOrWhiteSpace(paramList))
                func_line[^1] += paramList;     // don't want a space between function name and parenthesis
            else
                func_line.Add("()");        // this else never hits

            if (!string.IsNullOrWhiteSpace(constraints))
                func_line.Add(constraints);

            retVal.Add(string.Join(' ', func_line));

            return string.Join("\n", retVal);
        }

        private static string[] GetNamespaceParts(NameSyntax name)
        {
            var parts = new List<string>();

            if (name is QualifiedNameSyntax qualifiedName)
            {
                parts.AddRange(GetNamespaceParts(qualifiedName.Left));
                parts.Add(qualifiedName.Right.GetText().ToString());
            }
            else
            {
                parts.Add(name.GetText().ToString());
            }

            for (int i = 0; i < parts.Count; i++)
                parts[i] = parts[i].Replace("\r", "").Replace("\n", "");        // only saw on the last one, but might as well check all of them

            return parts.ToArray();
        }
        private static CodeSnippet TextToSnippet(string text, long id)
        {
            int line_count = text.Count(c => c == '\n') + 1;        // the last line won't have a newline, but it will still be a line

            text = text.Replace("\r\n", "\n");

            return new CodeSnippet()
            {
                Type = CodeSnippetType.Error,
                UniqueID = id,

                LineFrom = 0,
                LineTo = line_count - 1,

                Text = text,
            };
        }

        private static string GetComments(SyntaxTriviaList trivia)
        {
            // There are a ton of enum values, hopefully these cover all types of comments
            if (trivia.Any(o => o.IsKind(SyntaxKind.SingleLineCommentTrivia) || o.IsKind(SyntaxKind.MultiLineCommentTrivia) || o.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) || o.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)))
                return CleanupSnippetText(trivia.ToString());       // there could be more, but trying to pick through the trivia list and only get comments is a lot of work.  was missing first /// of a /// <summary>

            return null;
        }

        private static string CleanupSnippetText(string text)
        {
            var lines = GetLines_list(text);

            // Remove Regions
            CleanupSnippetText_Regions(lines);

            // Remove leading/trailing newlines
            CleanupSnippetText_LeadingTrailingNewlines(lines);

            // Remove leading indents
            CleanupSnippetText_LeadingIndents(lines);

            return string.Join('\n', lines);
        }
        private static void CleanupSnippetText_Regions(List<string> lines)
        {
            int index = 0;
            while (index < lines.Count)
            {
                string line_trimmed = lines[index].Trim();

                if (line_trimmed.StartsWith("#region") || line_trimmed.StartsWith("#endregion"))        // since the class gets chopped up, region begin/end end up in different snippets.  It is difficult to know if the code is inside or outside a function, so remove them all (otherwise, could keep regions inside functions)
                    lines.RemoveAt(index);

                else
                    index++;
            }
        }
        private static void CleanupSnippetText_LeadingTrailingNewlines(List<string> lines)
        {
            if (lines.Count == 0)
                return;

            int index = 0;
            int nonspace_index = -1;
            while (index < lines.Count)
            {
                string line_trimmed = lines[index].Trim();

                if (line_trimmed == "")
                {
                    if (nonspace_index < 0)
                        lines.RemoveAt(index);      // blank line before nonblank
                    else
                        index++;
                }
                else
                {
                    nonspace_index = index;
                    index++;
                }
            }

            if (nonspace_index < 0)
                lines.Clear();      // probably cleared already, but making sure

            else
                for (int i = lines.Count - 1; i > nonspace_index; i--)
                    lines.RemoveAt(i);      // blank line at the end
        }
        private static void CleanupSnippetText_LeadingIndents(IList<string> lines)
        {
            if (lines.Count == 0)
                return;

            // Convert leading tabs to spaces (if some lines start with tabs, some with spaces, the later logic in
            // this function would fail)
            ConvertLeadingTabsToSpaces(lines);

            // Find the leftmost non whitespace
            int min_indent = FindMinIndent(lines);

            if (min_indent <= 0)
                return;     // it's already as far left as it can go

            // Remove the chars before min_indent from each line so that there is no extra indentation
            RemoveLeftChars(lines, min_indent - 1);
        }

        private static string RemoveAllLeadingIndents(string text)
        {
            string[] lines = GetLines_arr(text);

            for (int i = 0; i < lines.Length; i++)
            {
                int index = IndexOfFirstNonWhitespaceCharacter(lines[i]);

                if (index > 0)
                    lines[i] = lines[i].Substring(index);
            }

            return string.Join('\n', lines);
        }

        private static void ConvertLeadingTabsToSpaces(IList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                // Find index of first non whitespace
                int firstNonWhitespaceIndex = IndexOfFirstNonWhitespaceCharacter(lines[i]);
                if (firstNonWhitespaceIndex == -1)
                {
                    lines[i] = "";
                    continue;
                }

                // Only replace tabs to the left of the first non whitepace
                string left = lines[i].
                    Substring(0, firstNonWhitespaceIndex).
                    Replace("\t", "    ");

                // Put the two pieces together
                lines[i] = left + lines[i].Substring(firstNonWhitespaceIndex);
            }
        }

        private static int FindMinIndent(IList<string> lines)
        {
            int? min_indent = null;

            foreach (string line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                int firstNonWhitespaceIndex = IndexOfFirstNonWhitespaceCharacter(line);
                if (firstNonWhitespaceIndex == -1)
                    continue;

                if (min_indent == null || firstNonWhitespaceIndex < min_indent.Value)
                    min_indent = firstNonWhitespaceIndex;
            }

            return min_indent ?? 0;
        }

        private static void RemoveLeftChars(IList<string> lines, int remove_count)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                    continue;

                lines[i] = lines[i].Substring(remove_count + 1);
            }
        }

        private static string AddLeftChars(string text, int add_count)
        {
            var lines = GetLines_arr(text);

            string indent = new string(' ', add_count);

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrEmpty(lines[i]))
                    continue;

                lines[i] = indent + lines[i];
            }

            return string.Join("\n", lines);
        }

        private static int IndexOfFirstNonWhitespaceCharacter(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return -1;

            for (int i = 0; i <= text.Length; i++)
                if (!char.IsWhiteSpace(text[i]))
                    return i;

            return -1;
        }

        private static string[] GetLines_arr(string text)
        {
            return text?.
                Replace("\r", "").
                Split('\n');
        }
        private static List<string> GetLines_list(string text)
        {
            return text?.
                Replace("\r", "").
                Split('\n').
                ToList();
        }

        #endregion
    }
}

#region class hierarchy
/*

BaseArgumentListSyntax
    ArgumentListSyntax
    BracketedArgumentListSyntax

SwitchLabelSyntax
    CasePatternSwitchLabelSyntax
    CaseSwitchLabelSyntax
    DefaultSwitchLabelSyntax

BaseExpressionColonSyntax
    ExpressionColonSyntax
    NameColonSyntax

BaseCrefParameterListSyntax
    CrefParameterListSyntax
    CrefBracketedParameterListSyntax

BaseTypeSyntax
    SimpleBaseTypeSyntax
    PrimaryConstructorBaseTypeSyntax

TypeSyntax
    PredefinedTypeSyntax
    ArrayTypeSyntax
    PointerTypeSyntax
    FunctionPointerTypeSyntax
    NullableTypeSyntax
    TupleTypeSyntax
    OmittedTypeArgumentSyntax
    RefTypeSyntax
    ScopedTypeSyntax

InterpolatedStringContentSyntax
    InterpolatedStringTextSyntax
    InterpolationSyntax

ExpressionOrPatternSyntax
    ExpressionSyntax
        ParenthesizedExpressionSyntax
        TupleExpressionSyntax
        PrefixUnaryExpressionSyntax
        AwaitExpressionSyntax
        PostfixUnaryExpressionSyntax
        MemberAccessExpressionSyntax
        ConditionalAccessExpressionSyntax
        MemberBindingExpressionSyntax
        ElementBindingExpressionSyntax
        RangeExpressionSyntax
        ImplicitElementAccessSyntax
        BinaryExpressionSyntax
        AssignmentExpressionSyntax
        ConditionalExpressionSyntax
        InstanceExpressionSyntax
            ThisExpressionSyntax
            BaseExpressionSyntax
        LiteralExpressionSyntax
        FieldExpressionSyntax
        MakeRefExpressionSyntax
        RefTypeExpressionSyntax
        RefValueExpressionSyntax
        CheckedExpressionSyntax
        DefaultExpressionSyntax
        TypeOfExpressionSyntax
        SizeOfExpressionSyntax
        InvocationExpressionSyntax
        ElementAccessExpressionSyntax
        DeclarationExpressionSyntax
        CastExpressionSyntax
        AnonymousFunctionExpressionSyntax
            AnonymousMethodExpressionSyntax
            LambdaExpressionSyntax
                SimpleLambdaExpressionSyntax
                ParenthesizedLambdaExpressionSyntax
        RefExpressionSyntax
        InitializerExpressionSyntax
        BaseObjectCreationExpressionSyntax
            ImplicitObjectCreationExpressionSyntax
            ObjectCreationExpressionSyntax
        WithExpressionSyntax
        AnonymousObjectCreationExpressionSyntax
        ArrayCreationExpressionSyntax
        ImplicitArrayCreationExpressionSyntax
        StackAllocArrayCreationExpressionSyntax
        ImplicitStackAllocArrayCreationExpressionSyntax
        CollectionExpressionSyntax
        QueryExpressionSyntax
        OmittedArraySizeExpressionSyntax
        InterpolatedStringExpressionSyntax
        IsPatternExpressionSyntax
        ThrowExpressionSyntax
        SwitchExpressionSyntax
    PatternSyntax
        DiscardPatternSyntax
        DeclarationPatternSyntax
        VarPatternSyntax
        RecursivePatternSyntax
        ConstantPatternSyntax
        ParenthesizedPatternSyntax
        RelationalPatternSyntax
        TypePatternSyntax
        BinaryPatternSyntax
        UnaryPatternSyntax
        ListPatternSyntax
        SlicePatternSyntax

VariableDesignationSyntax
    SingleVariableDesignationSyntax
    DiscardDesignationSyntax
    ParenthesizedVariableDesignationSyntax

XmlAttributeSyntax
    XmlTextAttributeSyntax
    XmlCrefAttributeSyntax
    XmlNameAttributeSyntax

TypeParameterConstraintSyntax
    ConstructorConstraintSyntax
    ClassOrStructConstraintSyntax
    TypeConstraintSyntax
    DefaultConstraintSyntax
    AllowsConstraintClauseSyntax

BaseParameterSyntax
    ParameterSyntax
    FunctionPointerParameterSyntax

AllowsConstraintSyntax
    RefStructConstraintSyntax

CrefSyntax
    TypeCrefSyntax
    QualifiedCrefSyntax
    MemberCrefSyntax
        NameMemberCrefSyntax
        IndexerMemberCrefSyntax
        OperatorMemberCrefSyntax
        ConversionOperatorMemberCrefSyntax

BaseParameterListSyntax
    ParameterListSyntax
    BracketedParameterListSyntax

NameSyntax
    SimpleNameSyntax
        IdentifierNameSyntax
        GenericNameSyntax
    QualifiedNameSyntax
    AliasQualifiedNameSyntax

DirectiveTriviaSyntax
    BranchingDirectiveTriviaSyntax
        ConditionalDirectiveTriviaSyntax
            IfDirectiveTriviaSyntax
            ElifDirectiveTriviaSyntax
        ElseDirectiveTriviaSyntax
    EndIfDirectiveTriviaSyntax
    RegionDirectiveTriviaSyntax
    EndRegionDirectiveTriviaSyntax
    ErrorDirectiveTriviaSyntax
    WarningDirectiveTriviaSyntax
    BadDirectiveTriviaSyntax
    DefineDirectiveTriviaSyntax
    UndefDirectiveTriviaSyntax
    LineOrSpanDirectiveTriviaSyntax
        LineDirectiveTriviaSyntax
        LineSpanDirectiveTriviaSyntax
    PragmaWarningDirectiveTriviaSyntax
    PragmaChecksumDirectiveTriviaSyntax
    ReferenceDirectiveTriviaSyntax
    LoadDirectiveTriviaSyntax
    ShebangDirectiveTriviaSyntax
    NullableDirectiveTriviaSyntax

SelectOrGroupClauseSyntax
    SelectClauseSyntax
    GroupClauseSyntax

StatementSyntax
    BlockSyntax
    LocalFunctionStatementSyntax
    LocalDeclarationStatementSyntax
    ExpressionStatementSyntax
    EmptyStatementSyntax
    LabeledStatementSyntax
    GotoStatementSyntax
    BreakStatementSyntax
    ContinueStatementSyntax
    ReturnStatementSyntax
    ThrowStatementSyntax
    YieldStatementSyntax
    WhileStatementSyntax
    DoStatementSyntax
    ForStatementSyntax
    CommonForEachStatementSyntax
        ForEachStatementSyntax
        ForEachVariableStatementSyntax
    UsingStatementSyntax
    FixedStatementSyntax
    CheckedStatementSyntax
    UnsafeStatementSyntax
    LockStatementSyntax
    IfStatementSyntax
    SwitchStatementSyntax
    TryStatementSyntax

QueryClauseSyntax
    FromClauseSyntax
    LetClauseSyntax
    JoinClauseSyntax
    WhereClauseSyntax
    OrderByClauseSyntax

XmlNodeSyntax
    XmlElementSyntax
    XmlEmptyElementSyntax
    XmlTextSyntax
    XmlCDataSectionSyntax
    XmlProcessingInstructionSyntax
    XmlCommentSyntax

CollectionElementSyntax
    ExpressionElementSyntax
    SpreadElementSyntax

MemberDeclarationSyntax
    BaseNamespaceDeclarationSyntax
        NamespaceDeclarationSyntax
        FileScopedNamespaceDeclarationSyntax
    BaseTypeDeclarationSyntax
        TypeDeclarationSyntax
            ClassDeclarationSyntax
            StructDeclarationSyntax
            InterfaceDeclarationSyntax
            RecordDeclarationSyntax
        EnumDeclarationSyntax
    DelegateDeclarationSyntax
    EnumMemberDeclarationSyntax
    BaseFieldDeclarationSyntax
        FieldDeclarationSyntax
        EventFieldDeclarationSyntax
    BaseMethodDeclarationSyntax
        MethodDeclarationSyntax
        OperatorDeclarationSyntax
        ConversionOperatorDeclarationSyntax
        ConstructorDeclarationSyntax
        DestructorDeclarationSyntax
    BasePropertyDeclarationSyntax
        PropertyDeclarationSyntax
        EventDeclarationSyntax
        IndexerDeclarationSyntax
    IncompleteMemberSyntax

*/
#endregion