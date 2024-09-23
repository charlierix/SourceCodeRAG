using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    public static class Extensions
    {
        public static string ToJoin(this IEnumerable<string> values, string separator)
        {
            return string.Join(separator, values);
        }
    }
}
