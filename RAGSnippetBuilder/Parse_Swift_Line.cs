using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    public static class Parse_Swift_Line
    {
        #region enum: SpanType

        public enum SpanType
        {
            Other,
            Comment,
            String
        }

        #endregion
        #region record: CharSpan

        public record CharSpan
        {
            public SpanType Type { get; init; }

            public int IndexStart { get; init; }
            public int IndexEnd { get; init; }

            // Used when type is comment or string
            public BlockDelimiters Delimiters { get; init; }

            public string Text { get; init; }

            public override string ToString()
            {
                return $"{Type}: '{Text}'";
            }
        }

        #endregion
        #region record: BlockDelimiters

        public record BlockDelimiters
        {
            public string Start { get; init; }

            // If start is // then this would be null, meaning it's a comment for the rest of the line
            public string End { get; init; }

            // If populated, this before the end string means don't end the block
            // This is for things like double quote defining string.  Inside, \" is literal double quoate, so escape would be \
            public string Escape { get; init; }

            public bool IsMultiline { get; init; }
        }

        #endregion

        /// <summary>
        /// Breaks the line into spans of standard code, string literals, comments
        /// This makes it easy for other parsing logic to only look at the Other spans
        /// </summary>
        /// <remarks>
        /// Incoming state of Comment and String will only be in multi line scenarios /*   */  and  """    """
        /// In those cases, end_delimiter will hold the string needed to send the block
        /// </remarks>
        /// <returns>
        /// Need to return state and delimiters separately, because the list entry in spans could be a finished multi
        /// line string because there's nothing else in the line.  The actual state would be back to other
        /// </returns>
        public static (CharSpan[] spans, SpanType state, BlockDelimiters delimiters) ParseLine(string line, SpanType state, BlockDelimiters delimiters)
        {
            var retVal = new List<CharSpan>();

            int start_index = 0;

            for (int i = 0; i < line.Length; i++)
            {
                bool should_breakearly = false;

                switch (state)
                {
                    case SpanType.Other:
                        var result = Code_ExamineChar(line, i);
                        if (state != result.state)
                        {
                            // Went from other to string or comment.  Add the previous (other) block, then set the state to the string or comment
                            retVal.Add(new CharSpan()
                            {
                                Type = state,
                                IndexStart = start_index,
                                IndexEnd = i - 1,
                                Delimiters = result.delimiters,
                                Text = line.Substring(start_index, i - start_index),
                            });

                            state = result.state;
                            delimiters = result.delimiters;
                            start_index = i;

                            if (delimiters.Start.Length > 1)
                                i += delimiters.Start.Length - 1;       // some delimiters are multi char, so advance the index in those cases
                        }

                        // Double slash style comment means the rest of the line is a comment
                        if (state == SpanType.Comment && delimiters.End == null)
                            should_breakearly = true;
                        break;

                    case SpanType.String:
                    case SpanType.Comment:
                        if (NonCode_ExamineChar(line, i, delimiters.End, delimiters.Escape))
                        {
                            // The string or comment block has ended.  Add that span to the list, then go back to a standard code state
                            if (delimiters.End.Length > 1)
                                i += delimiters.End.Length - 1;       // some delimiters are multi char, so advance the index in those cases

                            retVal.Add(new CharSpan()
                            {
                                Type = state,
                                IndexStart = start_index,
                                IndexEnd = i,       // i is on the end delimiter.  next iteration of i will be a new block
                                Delimiters = delimiters,
                                Text = line.Substring(start_index, i + 1 - start_index),
                            });

                            state = SpanType.Other;
                            delimiters = null;
                            start_index = i + 1;
                        }
                        break;

                    default:
                        throw new ApplicationException($"Unknown SpanType: {state}");
                }

                if (should_breakearly)
                    break;
            }

            if (start_index < line.Length)
                retVal.Add(new CharSpan()
                {
                    Type = state,
                    IndexStart = start_index,
                    IndexEnd = line.Length - 1,
                    Delimiters = delimiters,
                    Text = line.Substring(start_index),
                });

            if(delimiters == null || !delimiters.IsMultiline)
            {
                state = SpanType.Other;
                delimiters = null;
            }

            return (retVal.ToArray(), state, delimiters);
        }

        /// <summary>
        /// Called when in normal code.  Looks for the start of a string or comment
        /// </summary>
        /// <returns>
        /// The new state
        /// NOTE: if it's a // comment, then end_delimiter will be null, meaning the rest of the line will be a comment
        /// </returns>
        private static (SpanType state, BlockDelimiters delimiters) Code_ExamineChar(string line, int index)
        {
            switch (line[index])
            {
                case '#':
                    Match match = Regex.Match(line.Substring(index), "^#+\"");
                    if (match.Success)
                    {
                        if (index + match.Length < line.Length - 2 && line[index + match.Length + 1] == '"' && line[index + match.Length + 1] == '"')
                            return (SpanType.String,
                                new BlockDelimiters()       // #"""    """# (or multiple pounds).  The three quotes allows it to span multi lines
                                {
                                    Start = match.Value + "\"\"",
                                    End = "\"\"\"" + new string('#', match.Length - 1),
                                    Escape = null,
                                    IsMultiline = true,
                                });
                        else
                            return (SpanType.String,
                                new BlockDelimiters()
                                {
                                    Start = match.Value,
                                    End = "\"" + new string('#', match.Length - 1),     // the match is #"  or maybe ###"   so the end would be "#   or "###
                                    Escape = null,
                                    IsMultiline = false,
                                });
                    }
                    else
                        return (SpanType.Other, null);

                case '\'':
                    return (SpanType.String,
                        new BlockDelimiters()
                        {
                            Start = "'",
                            End = "'",
                            Escape = "\"",
                            IsMultiline = false,
                        });

                case '"':
                    if (index < line.Length - 2 && line[index + 1] == '"' && line[index + 2] == '"')        // """ is a multiline string (similar to c# @")
                        return (SpanType.String,
                            new BlockDelimiters()
                            {
                                Start = "\"\"\"",
                                End = "\"\"\"",
                                Escape = null,
                                IsMultiline = true,
                            });
                    else
                        return (SpanType.String,
                            new BlockDelimiters()
                            {
                                Start = "\"",
                                End = "\"",
                                Escape = "\\",
                                IsMultiline = false,
                            });

                case '/':
                    if (index < line.Length - 1 && line[index + 1] == '/')
                        return (SpanType.Comment,
                            new BlockDelimiters()
                            {
                                Start = "//",
                                End = null,
                                Escape = null,
                                IsMultiline = false,
                            });

                    else if (index < line.Length - 1 && line[index + 1] == '*')
                        return (SpanType.Comment,
                            new BlockDelimiters()
                            {
                                Start = "/*",
                                End = "*/",
                                Escape = null,
                                IsMultiline = true,
                            });

                    else
                        return (SpanType.Other, null);

                default:
                    return (SpanType.Other, null);
            }
        }

        /// <summary>
        /// Called when inside of a string or comment.  Looks for the end of the current block
        /// </summary>
        /// <returns>
        /// True: char at index finished the block.  The next char will be SpanType.Other
        /// False: still inside the block
        /// </returns>
        private static bool NonCode_ExamineChar(string line, int index, string end_delimiter, string escape)
        {
            if (line[index] != end_delimiter[0])
                return false;

            // The first char of end_delimiter matches
            // See if the rest matches
            if (index + end_delimiter.Length > line.Length)
                return false;       // too near the end of the line

            for (int i = 1; i < end_delimiter.Length; i++)
                if (line[index + i] != end_delimiter[i])
                    return false;       // not the end delimiter

            // Check for escaped chars.  For example, in this line:
            // var = "hello \"there\"" + " some more text"
            //
            // The \ is the escape char and the string only ends when a non escaped " is seen
            if (escape != null && index >= escape.Length)
            {
                bool is_escape = true;

                for (int i = 0; i < escape.Length; i++)
                    if (line[index - escape.Length + i] != escape[i])
                    {
                        is_escape = false;
                        break;
                    }

                if (is_escape)
                    return false;
            }

            return true;
        }
    }
}
