using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    public static class Parse_Swift_Line2
    {
        public enum SpanType
        {
            Other,
            Comment,
            String
        }




        // TODO: make this a record
        // TODO: include the string that this spans

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

        public static CharSpan[] ParseLine(string line, (bool IsInComment, bool IsInString, char StringDelimiter) previousState)
        {
            var spans = new List<CharSpan>();
            int currentSpanStartIndex = 0;
            SpanType currentSpanType = SpanType.Other;
            bool isInComment = previousState.IsInComment;
            bool isInString = previousState.IsInString;


            // TODO: I think delimiter should be string instead of char
            char stringDelimiter = previousState.StringDelimiter;



            for (int i = 0; i < line.Length; i++)
            {
                char character = line[i];

                if (!isInComment && !isInString)
                {
                    // Not inside a comment or string literal, check for delimiters
                    switch (character)
                    {



                        // TODO: this is a string, not a comment
                        case '#':
                            // Single-line comment start
                            spans.Add(new CharSpan(currentSpanStartIndex, i, currentSpanType));
                            spans.Add(new CharSpan(i, line.Length, SpanType.Comment));
                            return spans.ToArray();




                        case '"' when i < line.Length - 2 && line[i + 1] == '"' && line[i + 2] == '"':
                            // Multi-line string start
                            isInString = true;
                            stringDelimiter = '"';
                            spans.Add(new CharSpan(currentSpanStartIndex, i, currentSpanType));
                            currentSpanStartIndex = i;
                            currentSpanType = SpanType.String;
                            break;

                        case '\'':
                            // Single-line string start
                            isInString = true;
                            stringDelimiter = '\'';
                            spans.Add(new CharSpan(currentSpanStartIndex, i, currentSpanType));
                            currentSpanStartIndex = i;
                            currentSpanType = SpanType.String;
                            break;
                    }
                }
                else if (isInComment && character == '\n')
                {
                    // Inside a comment, check for end delimiter (newline)
                    isInComment = false;
                    spans.Add(new CharSpan(currentSpanStartIndex, i + 1, SpanType.Comment));
                    currentSpanStartIndex = i + 1;
                    currentSpanType = SpanType.Other;
                }
                else if (isInString)
                {

                    // TODO: when inside of string, escaped chars need to be accounted for


                    // Inside a string literal, check for end delimiter
                    switch (character)
                    {
                        case '"' when stringDelimiter == '"' && i < line.Length - 2 && line[i + 1] == '"' && line[i + 2] == '"':
                            // Multi-line string end
                            isInString = false;
                            spans.Add(new CharSpan(currentSpanStartIndex, i + 3, SpanType.String));
                            currentSpanStartIndex = i + 3;
                            currentSpanType = SpanType.Other;
                            break;

                        case '\'' when stringDelimiter == '\'':
                            // Single-line string end
                            isInString = false;
                            spans.Add(new CharSpan(currentSpanStartIndex, i + 1, SpanType.String));
                            currentSpanStartIndex = i + 1;
                            currentSpanType = SpanType.Other;
                            break;
                    }
                }
            }

            // Handle unclosed comments or strings at the end of line
            if (isInComment)
            {
                spans.Add(new CharSpan(currentSpanStartIndex, line.Length, SpanType.Comment));
            }
            else if (isInString)
            {
                spans.Add(new CharSpan(currentSpanStartIndex, line.Length, SpanType.String));
            }
            else
            {
                spans.Add(new CharSpan(currentSpanStartIndex, line.Length, currentSpanType));
            }

            return spans.ToArray();
        }

    }
}
