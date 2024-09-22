using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    public static class Parse_Swift_Line1
    {
        public enum SpanType
        {
            Other,
            Comment,
            String
        }

        public struct CharSpan
        {
            public int StartIndex { get; }
            public int EndIndex { get; }
            public SpanType Type { get; }

            public CharSpan(int startIndex, int endIndex, SpanType type)
            {
                StartIndex = startIndex;
                EndIndex = endIndex;
                Type = type;
            }
        }

        public static CharSpan[] ParseLine(string line, (bool IsInComment, bool IsInString) previousState)
        {
            var spans = new List<CharSpan>();
            int currentSpanStartIndex = 0;
            SpanType currentSpanType = SpanType.Other;
            bool isInComment = previousState.IsInComment;
            bool isInString = previousState.IsInString;

            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];

                if (!isInComment && !isInString)
                {
                    // Not inside a comment or string literal, check for delimiters
                    switch (character)
                    {
                        case '/' when i < line.Length - 1 && line[i + 1] == '/':
                            // Single-line comment start
                            spans.Add(new CharSpan(currentSpanStartIndex, i, currentSpanType));
                            spans.Add(new CharSpan(i, line.Length, SpanType.Comment));
                            return spans.ToArray();

                        case '/' when i < line.Length - 1 && line[i + 1] == '*':
                            // Multi-line comment start
                            isInComment = true;
                            spans.Add(new CharSpan(currentSpanStartIndex, i, currentSpanType));
                            currentSpanStartIndex = i;
                            currentSpanType = SpanType.Comment;
                            break;
                        case '\"':
                        case '\'':
                            // String literal start
                            isInString = true;
                            spans.Add(new CharSpan(currentSpanStartIndex, i, currentSpanType));
                            currentSpanStartIndex = i;
                            currentSpanType = SpanType.String;
                            break;
                    }
                }
                else if (isInComment)
                {
                    // Inside a comment, check for end delimiter
                    switch (character)
                    {
                        case '*' when i < line.Length - 1 && line[i + 1] == '/':
                            // Multi-line comment end
                            isInComment = false;
                            spans.Add(new CharSpan(currentSpanStartIndex, i + 1, SpanType.Comment));
                            currentSpanStartIndex = i + 1;
                            currentSpanType = SpanType.Other;
                            break;
                    }
                }
                else if (isInString)
                {
                    // Inside a string literal, check for end delimiter
                    switch (character)
                    {
                        case '\"':
                        case '\'':
                            // String literal end (if it matches the start delimiter)
                            isInString = false;
                            spans.Add(new CharSpan(currentSpanStartIndex, i + 1, SpanType.String));
                            currentSpanStartIndex = i + 1;
                            currentSpanType = SpanType.Other;
                            break;
                    }
                }
            }

            // Append any remaining span at the end of the line
            spans.Add(new CharSpan(currentSpanStartIndex, line.Length, currentSpanType));

            return spans.ToArray();
        }
    }
}
