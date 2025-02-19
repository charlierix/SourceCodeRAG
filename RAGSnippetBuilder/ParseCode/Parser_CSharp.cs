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
        #region class: CodeSnippetVisitor

        private class CodeSnippetVisitor : CSharpSyntaxVisitor
        {
            public List<CodeSnippet> Snippets { get; } = new();
            private string _currentNamespace;
            private string _currentClassName;

            private readonly Func<long> _get_next_id;

            public CodeSnippetVisitor(Func<long> get_next_id)
            {
                _get_next_id = get_next_id;
            }

            public override void Visit(SyntaxNode node)
            {

                // the base class seems to be CompilationUnitSyntax, which has a .Members property
                CompilationUnitSyntax test;


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


                // note in the tree above, only the base class's .Members is reported.  derived classes have an override, but I'm not
                // including that

                // so it looks like once you're at a TypeDeclarationSyntax, all members below will be fields, methods, etc (or other
                // types that need further recursion)



                // so the llm is trying to get me to use this visitor class and put a type checker here, iterating the .Members, calling
                // Visit() for each child

                // maybe that's a better design, or maybe just write a completely custom parser...



                base.Visit(node);
            }

            public override void VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
            {
                string namespaceName = string.Join(".", GetNamespaceParts(node.Name));

                // Recurse into child nodes
                base.VisitNamespaceDeclaration(node);

                _currentNamespace = namespaceName;
            }

            public override void VisitClassDeclaration(ClassDeclarationSyntax node)
            {
                var modifiers = string.Join(" ", node.Modifiers.Select(m => m.Text));
                var className = node.Identifier.Text;
                var baseTypes = node.BaseList?.ToString() ?? "";

                // Update current context
                _currentClassName = className;

                // Add class snippet
                Snippets.Add(new CodeSnippet()
                {
                    UniqueID = _get_next_id(),
                    LineFrom = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                    LineTo = node.GetLocation().GetLineSpan().EndLinePosition.Line,
                    NameSpace = _currentNamespace,
                    ParentName = null, // No parent for top-level class
                    Inheritance = baseTypes,
                    Name = className,
                    Type = CodeSnippetType.Class,
                    Text = node.GetText().ToString(),
                });

                // Recurse into child nodes
                base.VisitClassDeclaration(node);
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                var methodName = node.Identifier.Text;
                var methodModifiers = string.Join(" ", node.Modifiers.Select(m => m.Text));
                var returnType = node.ReturnType?.ToString() ?? "void";

                // Add method snippet
                Snippets.Add(new CodeSnippet()
                {
                    UniqueID = _get_next_id(),
                    LineFrom = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                    LineTo = node.GetLocation().GetLineSpan().EndLinePosition.Line,
                    NameSpace = _currentNamespace,
                    ParentName = _currentClassName,
                    Inheritance = null, // No inheritance for methods
                    Name = methodName,
                    Type = CodeSnippetType.Func,
                    Text = node.GetText().ToString(),
                });

                base.VisitMethodDeclaration(node);
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

                return parts.ToArray();
            }
        }

        #endregion

        public static CodeFile Parse(FilePathInfo filepath, Func<long> get_next_id)
        {
            string file_contents = File.ReadAllText(filepath.FullFilename);

            var syntaxTree = CSharpSyntaxTree.ParseText(file_contents);


            // TODO: may need to create error nodes if this is true
            //var test = syntaxTree.GetDiagnostics().ToArray();
            //if (syntaxTree.GetDiagnostics().Any(o => o.Severity == DiagnosticSeverity.Error))
            //    throw new System.InvalidOperationException("Errors parsing file");


            CodeSnippet[] snippets = null;
            var props = new Dictionary<long, List<CodeSnippet>>();       // these are properties, fields, etc (not enums or functions) by parent class ID.  They will get added to the class at the end

            if (syntaxTree.TryGetRoot(out SyntaxNode root))
            {
                // Doesn't work
                //var visitor = new CodeSnippetVisitor(get_next_id);
                //visitor.Visit(syntaxTree.GetRoot());
                //snippets = visitor.Snippets.ToArray();

                snippets = ParseNode(syntaxTree.GetRoot(), null, null, get_next_id, props);

            }
            //else if (syntaxTree.TryGetText(out SourceText text))      // more headache than it's worth to try to covert that block into a string
            //    snippets = [TextToSnippet(text, get_next_id())];
            else
                snippets = [TextToSnippet(file_contents, get_next_id())];




            // TODO: find all classes and rebuild to look like interfaces (fields/props, function definitions)
            RebuildClasses(snippets, props);


            return CodeFile.BuildOuter(filepath) with
            {
                Snippets = snippets,
            };
        }

        #region Private Methods

        private static CodeSnippet[] ParseNode(SyntaxNode node, CodeSnippet parent, string ns_text, Func<long> get_next_id, Dictionary<long, List<CodeSnippet>> props)
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
                    retVal.AddRange(ParseNode(comp_member, parent, ns_text, get_next_id, props));
            }
            else if (node is BaseNamespaceDeclarationSyntax ns)
            {
                ns_text = string.Join(".", GetNamespaceParts(ns.Name));

                foreach (var ns_member in ns.Members)
                    retVal.AddRange(ParseNode(ns_member, parent, ns_text, get_next_id, props));
            }
            else if (node is TypeDeclarationSyntax type)
            {
                CodeSnippet snippet = BuildSnippet_Class(type, parent, ns_text, get_next_id);
                retVal.Add(snippet);

                foreach (var type_member in type.Members)
                    retVal.AddRange(ParseNode(type_member, snippet, ns_text, get_next_id, props));
            }
            else if (node is EnumDeclarationSyntax enm)
            {
                retVal.Add(BuildSnippet_Enum(enm, parent, ns_text, get_next_id));
            }
            else if (node is BaseMethodDeclarationSyntax method)
            {
                retVal.Add(BuildSnippet_Function(method, parent, ns_text, get_next_id));
            }
            else if (node is BaseFieldDeclarationSyntax field)
            {
                BuildSnippet_Field_Prop(field, field.GetText().ToString(), parent, ns_text, get_next_id, props);
            }
            else if (node is BasePropertyDeclarationSyntax prop)
            {
                BuildSnippet_Field_Prop(prop, prop.GetText().ToString(), parent, ns_text, get_next_id, props);
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

        private static CodeSnippet BuildSnippet_Class(TypeDeclarationSyntax node, CodeSnippet parent, string ns_text, Func<long> get_next_id)
        {
            return BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Class,

                Name = node.Identifier.Text,
                Inheritance = node.BaseList?.ToString(),

                // Text will be filled out in a post scan

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
        private static CodeSnippet BuildSnippet_Function(BaseMethodDeclarationSyntax node, CodeSnippet parent, string ns_text, Func<long> get_next_id)
        {
            string name = null;
            if (node is MethodDeclarationSyntax method)
                name = method.Identifier.Text;
            else if (node is ConstructorDeclarationSyntax construct)
                name = construct.Identifier.Text;
            else if (node is DestructorDeclarationSyntax destruct)
                name = destruct.Identifier.Text;

            return BuildSnippet_Common(node, parent, ns_text, get_next_id) with
            {
                Type = CodeSnippetType.Func,

                Name = name,

                Arguments = node.ParameterList.ToString(),

                Text = CleanupSnippetText(node.GetText().ToString()),
            };
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
                Type = CodeSnippetType.Class,       // there is no enum for fields/props, but they will end up in the parent class

                Text = CleanupSnippetText(text),
            };

            if (!props.ContainsKey(parent.UniqueID))
                props.Add(parent.UniqueID, new List<CodeSnippet>());

            props[parent.UniqueID].Add(snippet);
        }

        private static CodeSnippet BuildSnippet_Common(SyntaxNode node, CodeSnippet parent, string ns_text, Func<long> get_next_id)
        {
            string modifiers = null;
            if (node is MemberDeclarationSyntax memdec)
                modifiers = string.Join(" ", memdec.Modifiers.Select(o => o.Text));

            return new CodeSnippet()
            {
                UniqueID = get_next_id(),
                ParentID = parent?.UniqueID,

                ParentName = parent?.Name,

                LineFrom = node.GetLocation().GetLineSpan().StartLinePosition.Line,
                LineTo = node.GetLocation().GetLineSpan().EndLinePosition.Line,

                NameSpace = ns_text,
                Modifiers = modifiers,
            };
        }

        private static void RebuildClasses(CodeSnippet[] snippets, Dictionary<long, List<CodeSnippet>> props)
        {
            for (int i = 0; i < snippets.Length; i++)
            {
                if (snippets[i].Type != CodeSnippetType.Class)
                    continue;

                StringBuilder class_text = new StringBuilder();

                // TODO: this should be the text
                class_text.AppendLine(RebuildClasses_ClassDefinition(snippets[i]));
                class_text.AppendLine("{");

                // Add all props
                if (props.TryGetValue(snippets[i].UniqueID, out List<CodeSnippet> class_props))
                {
                    foreach (var prop in class_props)
                    {
                        class_text.AppendLine(prop.Text);
                        class_text.AppendLine();
                    }
                }

                // TODO: add function headers


                class_text.AppendLine("}");


                snippets[i] = snippets[i] with
                {
                    Text = class_text.ToString(),
                };
            }
        }

        private static string RebuildClasses_ClassDefinition(CodeSnippet snippet)
        {
            string inheritance = string.IsNullOrWhiteSpace(snippet.Inheritance) ?
                "" :
                " : " + snippet.Inheritance;

            return $"{snippet.Modifiers} class {snippet.Name}{inheritance}";        // for now, just hardcode to class.  This function shouldn't be around long
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

        private static string CleanupSnippetText(string text)
        {
            var lines = text.
                Replace("\r\n", "\n").
                Split('\n').
                ToList();

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

        private static int IndexOfFirstNonWhitespaceCharacter(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return -1;

            for (int i = 0; i <= text.Length; i++)
                if (!char.IsWhiteSpace(text[i]))
                    return i;

            return -1;
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