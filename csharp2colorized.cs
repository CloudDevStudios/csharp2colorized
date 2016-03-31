﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using System.Text;
using System.Net;

namespace CSharp2Colorized
{

    public class ColorizedWord
    {
        public string Text;
        public int Red;
        public int Green;
        public int Blue;
        public bool IsItalic;

        public override string ToString() => Text;
    }

    public class ColorizedLine
    {
        public List<ColorizedWord> Words = new List<ColorizedWord>();
        public override string ToString() => string.Join("", Words.Select(w => w.Text));
    }


    public class CSharp2Colorized
    {
        private static int Main(string[] args)
        {
            if (args.Count() == 0)
            {
                // pipe
                using (var stream = new StreamReader(Console.OpenStandardInput()))
                {
                    var code = stream.ReadToEnd();
                    Console.WriteLine(Lines2Html(ColorizeCSharp(code)));
                    return 0;
                }
            }

            foreach (var fn in args)
            {
                if (args.Count() > 1) Console.WriteLine($"<h1>{WebUtility.HtmlEncode(Path.GetFileName(fn))}</h1>");
                if (!File.Exists(fn)) {Console.Error.WriteLine($"Not found - {fn}"); return 1;}
                var code = File.ReadAllText(fn);
                Console.WriteLine(Lines2Html(ColorizeCSharp(code)));
            }
            return 0;
        }

        /// <summary>
        /// Colorizes a fragment of C# code
        /// </summary>
        public static IEnumerable<ColorizedLine> ColorizeCSharp(string code) =>
            Words2Lines(CSharpSpecific.CSharpColorizingWalker.ColorizeInternal(code));

        public static IEnumerable<ColorizedLine> ColorizePlainText(string src) =>
            Words2Lines(ColorizePlainTextInternal(src));

        public static string Lines2Html(IEnumerable<ColorizedLine> lines)
        {
            var sb = new StringBuilder();
            sb.Append("<pre>");
            foreach (var line in lines)
            {
                foreach (var word in line.Words)
                {
                    var color = $"color:#{word.Red:x2}{word.Green:x2}{word.Blue:x2}";
                    if (color == "color:#000000") color = null;
                    if (word.IsItalic) sb.Append("<em>");
                    if (color != null) sb.Append($"<span style=\"{color}\">");
                    sb.Append(WebUtility.HtmlEncode(word.Text));
                    if (color != null) sb.Append("</span>");
                    if (word.IsItalic) sb.Append("</em>");
                }
                sb.AppendLine();
            }
            sb.AppendLine("</pre>");
            return sb.ToString();
        }

        /// <summary>
        /// Helper function: takes an load of words (with "null" representing a linebreak) and turns them into lines,
        /// minimizing along the way by combining adjacent words of the same color
        /// </summary>
        public static IEnumerable<ColorizedLine> Words2Lines(IEnumerable<ColorizedWord> words)
        {
            var encounteredFirstLinebreak = false;
            var currentLine = new ColorizedLine();
            var currentWord = null as ColorizedWord;
            foreach (var nextWord in words)
            {
                if (nextWord == null) // linebreak
                {
                    if (currentWord != null)
                    {
                        currentWord.Text = currentWord.Text.TrimEnd();
                        if (!string.IsNullOrWhiteSpace(currentWord.Text)) currentLine.Words.Add(currentWord);
                    }
                    if (encounteredFirstLinebreak || currentLine.Words.Count > 0) yield return currentLine;
                    encounteredFirstLinebreak = true;
                    currentLine = new ColorizedLine();
                    currentWord = null;
                }
                else if (currentWord == null) // first word on line
                {
                    currentWord = nextWord;
                }
                else if (string.IsNullOrWhiteSpace(currentWord.Text)) // merge the currentWord into the new one
                {
                    nextWord.Text = currentWord.Text + nextWord.Text;
                    currentWord = nextWord;
                }
                else if (string.IsNullOrWhiteSpace(nextWord.Text)) // merge the word into the previous one
                {
                    currentWord.Text = currentWord.Text + nextWord.Text;
                }
                else if (currentWord.Red == nextWord.Red && currentWord.Green == nextWord.Green && currentWord.Blue == nextWord.Blue && currentWord.IsItalic == nextWord.IsItalic)
                {
                    currentWord.Text = currentWord.Text + nextWord.Text;
                }
                else
                {
                    currentLine.Words.Add(currentWord);
                    currentWord = nextWord;
                }
            }
            if (currentWord != null)
            {
                currentWord.Text = currentWord.Text.TrimEnd();
                if (!string.IsNullOrWhiteSpace(currentWord.Text)) currentLine.Words.Add(currentWord);
            }
            if (currentLine.Words.Count > 0) yield return currentLine;
        }


        private static IEnumerable<ColorizedWord> ColorizePlainTextInternal(string src)
        {
            var lines = src.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            if (lines.Last() == "") lines.RemoveAt(lines.Count - 1);
            foreach (var line in lines)
            {
                if (line != "") yield return new ColorizedWord { Text = line };
                yield return null;
            }
        }

    }

    namespace CSharpSpecific
    {
        using Microsoft.CodeAnalysis.CSharp;
        using Microsoft.CodeAnalysis.CSharp.Syntax;

        internal class CSharpColorizingWalker : CSharpSyntaxWalker
        {
            // This code is based on that of Shiv Kumar at http://www.matlus.com/c-to-html-syntax-highlighter-using-roslyn/

            internal static IEnumerable<ColorizedWord> ColorizeInternal(string code)
            {
                code = code.Replace("...", "___threedots___"); // because ... is unusually hard to parse
                code = code.Replace("from *", "from ___linq_transparent_asterisk___"); // transparent identifier doesn't parse
                var ref_mscorlib = MetadataReference.CreateFromFile(typeof(object).GetTypeInfo().Assembly.Location);
                var ref_system = MetadataReference.CreateFromFile(typeof(Uri).GetTypeInfo().Assembly.Location);
                var ref_systemcore = MetadataReference.CreateFromFile(typeof(Enumerable).GetTypeInfo().Assembly.Location);
                var ref_systemcollectionsimmutable = MetadataReference.CreateFromFile(typeof(ImmutableArray<>).GetTypeInfo().Assembly.Location);
                var parse_options = new CSharpParseOptions(kind: SourceCodeKind.Script);
                var compile_options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, usings: new[] { "System", "System.Collections", "System.Collections.Generic" });
                var compilationUnit = SyntaxFactory.ParseCompilationUnit(code, options: parse_options);
                var syntaxTree = compilationUnit.SyntaxTree;
                var compilation = CSharpCompilation.Create("dummyAssemblyName", new[] { syntaxTree }, new[] { ref_mscorlib, ref_system, ref_systemcore, ref_systemcollectionsimmutable }, compile_options);
                var semanticModel = compilation.GetSemanticModel(syntaxTree, true);
                var w = new CSharpColorizingWalker() { sm = semanticModel };
                w.Visit(syntaxTree.GetRoot());
                //
                foreach (var word in w.Words)
                {
                    if (word == null) yield return word;
                    else if (word.Text == "___linq_transparent_asterisk___") yield return new ColorizedWord { Text = "*" };
                    else if (word.Text == "___threedots___") yield return new ColorizedWord { Text = "..." };
                    else
                    {
                        word.Text = word.Text.Replace("___linq_transparent_asterisk___", "*");
                        word.Text = word.Text.Replace("___threedots___", "...");
                        yield return word;
                    }
                }
            }

            private List<ColorizedWord> Words = new List<ColorizedWord>();
            private SemanticModel sm;
            private CSharpColorizingWalker() : base(SyntaxWalkerDepth.StructuredTrivia) { }

            public override void VisitToken(SyntaxToken token)
            {
                VisitTriviaList(token.LeadingTrivia);

                IEnumerable<ColorizedWord> r = null;

                if (token.IsKeyword())
                {
                    r = Col(token.Text, "Keyword");
                }
                else if (token.IsKind(SyntaxKind.StringLiteralToken))
                {
                    r = Col(token.Text, "StringLiteral");
                }
                else if (token.IsKind(SyntaxKind.CharacterLiteralToken))
                {
                    r = Col(token.Text, "StringLiteral");
                }
                else if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent is TypeParameterSyntax)
                {
                    r = Col(token.Text, "UserType");
                }
                else if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent is SimpleNameSyntax)
                {
                    var name = token.Parent as SimpleNameSyntax;
                    ISymbol symbol = null;
                    try { symbol = sm?.GetSymbolInfo(name).Symbol; }
                    catch (NullReferenceException) { }
                    // https://github.com/dotnet/roslyn/issues/10023 this might throw NRE, even though it shouldn't...
                    if (symbol?.Kind == SymbolKind.NamedType
                        || symbol?.Kind == SymbolKind.TypeParameter)
                    {
                        r = Col(token.Text, "UserType");
                    }
                    else if (symbol?.Kind == SymbolKind.DynamicType)
                    {
                        r = Col(token.Text, "Keyword");
                    }
                    else if (symbol?.Kind == SymbolKind.Namespace
                        || symbol?.Kind == SymbolKind.Parameter
                        || symbol?.Kind == SymbolKind.Local
                        || symbol?.Kind == SymbolKind.Field
                        || symbol?.Kind == SymbolKind.Property)
                    {
                        r = Col(token.Text, "PlainText");
                    }
                    else if (name.Identifier.Text == "var")
                    {
                        r = Col(token.Text, "Keyword");
                    }
                    else if (new[] { "C", "T", "U", "V" }.Contains(name.Identifier.Text))
                    {
                        r = Col(token.Text, "UserType");
                    }
                }
                else if (token.IsKind(SyntaxKind.IdentifierToken) && token.Parent is TypeDeclarationSyntax)
                {
                    var name = token.Parent as TypeDeclarationSyntax;
                    var symbol = sm.GetDeclaredSymbol(name);
                    if (symbol?.Kind == SymbolKind.NamedType)
                    {
                        r = Col(token.Text, "UserType");
                    }
                }

                if (r == null)
                {
                    if ((token.Parent as EnumDeclarationSyntax)?.Identifier == token)
                    {
                        r = Col(token.Text, "UserType");
                    }
                    else if ((token.Parent as GenericNameSyntax)?.Identifier == token)
                    {
                        if ((token.Parent.Parent as InvocationExpressionSyntax)?.Expression == token.Parent // e.g. "G<X>(1)"
                            || (token.Parent.Parent.Parent as InvocationExpressionSyntax)?.Expression == token.Parent.Parent) // e.g. e.G<X>(1)
                        {
                            r = Col(token.Text, "PlainText");
                        }
                        else // all other G<A> will assume that G is a type
                        {
                            r = Col(token.Text, "UserType");
                        }
                    }
                    else if (token.Parent.IsKind(SyntaxKind.GenericName))
                    {
                        if (token.Parent.Parent.IsKind(SyntaxKind.VariableDeclaration) // e.g. "private static readonly HashSet patternHashSet = New HashSet();" the first HashSet in this case
                            || token.Parent.Parent.IsKind(SyntaxKind.ObjectCreationExpression)) // e.g. "private static readonly HashSet patternHashSet = New HashSet();" the second HashSet in this case
                        {
                            r = Col(token.Text, "UserType");
                        }
                    }
                    else if (token.Parent.IsKind(SyntaxKind.IdentifierName))
                    {
                        if (token.Parent.Parent.IsKind(SyntaxKind.Parameter)
                            || token.Parent.Parent.IsKind(SyntaxKind.Attribute)
                            || token.Parent.Parent.IsKind(SyntaxKind.CatchDeclaration)
                            || token.Parent.Parent.IsKind(SyntaxKind.ObjectCreationExpression)
                            || token.Parent.Parent.IsKind(SyntaxKind.MethodDeclaration)
                            || token.Parent.Parent.IsKind(SyntaxKind.BaseList) // e.g. "public sealed class BuilderRouteHandler  IRouteHandler" IRouteHandler in this case
                            || token.Parent.Parent.Parent.IsKind(SyntaxKind.TypeOfExpression) // e.g. "Type baseBuilderType = TypeOf(BaseBuilder);" BaseBuilder in this case
                            || token.Parent.Parent.IsKind(SyntaxKind.VariableDeclaration) // e.g. "private DbProviderFactory dbProviderFactory;" Or "DbConnection connection = dbProviderFactory.CreateConnection();"
                            || token.Parent.Parent.IsKind(SyntaxKind.TypeArgumentList)) // e.g. "DbTypes = New Dictionary();" DbType in this case
                        {
                            r = Col(token.Text, "UserType");
                        }
                        else if ((token.Parent.Parent as CastExpressionSyntax)?.Type == token.Parent // e.g. "(Foo)x" the Foo
                            || (token.Parent.Parent as TypeConstraintSyntax)?.Type == token.Parent // e.g. "where T:Foo" the Foo
                            || (token.Parent.Parent as ArrayTypeSyntax)?.ElementType == token.Parent) // e.g. "Foo[]" the Foo
                        {
                            r = Col(token.Text, "UserType");
                        }
                        else if ((token.Parent.Parent.IsKind(SyntaxKind.ForEachStatement) && token.GetNextToken().Kind() != SyntaxKind.CloseParenToken)
                            || (token.Parent.Parent.Parent.IsKind(SyntaxKind.CaseSwitchLabel) && token.GetPreviousToken().Kind() != SyntaxKind.DotToken)
                            || (token.Parent.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) && token.Parent.Parent.Parent.IsKind(SyntaxKind.Argument) && !token.GetPreviousToken().IsKind(SyntaxKind.DotToken) && token.Text.Length > 0 && !char.IsLower(token.Text[0])) // e.g. "DbTypes.Add("int", DbType.Int32);" DbType in this case
                            || (token.Parent.Parent.IsKind(SyntaxKind.SimpleMemberAccessExpression) && !token.GetPreviousToken().IsKind(SyntaxKind.DotToken) && token.Text.Length > 0 && !char.IsLower(token.Text[0])))
                        {
                            r = Col(token.Text, "UserType");
                        }
                    }
                }

                if (r == null && !string.IsNullOrEmpty(token.Text)) // Empty comes from EndOfFile, OmmittedToken, ...
                {
                    r = Col(token.Text, "PlainText");
                }

                if (r != null) Words.AddRange(r);

                VisitTriviaList(token.TrailingTrivia);
            }


            void VisitTriviaList(SyntaxTriviaList trivias)
            {
                foreach (var trivia in trivias)
                {
                    var txt = trivia.ToFullString();

                    if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                    {
                        Words.Add(null);
                    }
                    else if (trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
                        || trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
                        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
                        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia))
                    {
                        Words.AddRange(Col(txt, "Comment"));
                    }
                    else if (trivia.IsKind(SyntaxKind.DisabledTextTrivia))
                    {
                        Words.AddRange(Col(txt, "ExcludedCode"));
                    }
                    else if (trivia.IsDirective)
                    {
                        Words.AddRange(Col(txt, "Preprocessor"));
                    }
                    else
                    {
                        Words.AddRange(Col(txt, "PlainText"));
                    }
                }
            }

            private IEnumerable<ColorizedWord> Col(string token, string color)
            {
                var isFirstLine = true;
                foreach (var txt in token.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None))
                {
                    if (isFirstLine) isFirstLine = false; else yield return null;
                    //
                    if (txt == "") continue;
                    else if (color == "PlainText") yield return new ColorizedWord { Text = txt };
                    else if (color == "Keyword") yield return new ColorizedWord { Text = txt, Blue = 255 };
                    else if (color == "UserType") yield return new ColorizedWord { Text = txt, Red = 43, Green = 145, Blue = 175 };
                    else if (color == "StringLiteral") yield return new ColorizedWord { Text = txt, Red = 163, Green = 21, Blue = 21 };
                    else if (color == "Comment") yield return new ColorizedWord { Text = txt, Green = 128 };
                    else if (color == "ExcludedCode") yield return new ColorizedWord { Text = txt, Red = 128, Green = 128, Blue = 128 };
                    else if (color == "Preprocessor") yield return new ColorizedWord { Text = txt, Red = 163, Green = 21, Blue = 128 };
                    else throw new Exception("bad color name");
                }
            }
        }

    }

}