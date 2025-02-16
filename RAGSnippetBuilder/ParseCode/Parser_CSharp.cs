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
using static Azure.Core.HttpHeader;

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

            if (syntaxTree.TryGetRoot(out SyntaxNode root))
            {
                // Doesn't work
                //var visitor = new CodeSnippetVisitor(get_next_id);
                //visitor.Visit(syntaxTree.GetRoot());
                //snippets = visitor.Snippets.ToArray();

                snippets = ParseNode(syntaxTree.GetRoot(), get_next_id);

            }
            //else if (syntaxTree.TryGetText(out SourceText text))      // more headache than it's worth to try to covert that block into a string
            //    snippets = [TextToSnippet(text, get_next_id())];
            else
                snippets = [TextToSnippet(file_contents, get_next_id())];

            return CodeFile.BuildOuter(filepath) with
            {
                Snippets = snippets,
            };
        }

        #region Private Methods

        private static CodeSnippet[] ParseNode(SyntaxNode node, Func<long> get_next_id)
        {
            var retVal = new List<CodeSnippet>();

            if (node is CompilationUnitSyntax comp)
            {

            }
            else if (node is BaseNamespaceDeclarationSyntax ns)
            {

            }
            else if (node is TypeDeclarationSyntax type)
            {

            }
            else
            {

            }

            return retVal.ToArray();
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