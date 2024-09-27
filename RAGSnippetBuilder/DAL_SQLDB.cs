using Microsoft.Data.Sqlite;
using RAGSnippetBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    public class DAL_SQLDB
    {
        #region record: PendingSnippet

        private record PendingSnippet
        {
            public string Folder { get; init; }
            public string File { get; init; }
            public CodeSnippet Snippet { get; init; }
        }

        #endregion

        #region Declaration Section

        private readonly string _connectionString;

        private readonly int _batchSize;
        private readonly List<PendingSnippet> _pendingSnippets = new List<PendingSnippet>();

        private long _token = 1000;     // start it a little higher so it's not confused with the primary key of the table

        #endregion

        public DAL_SQLDB(string folder, int batch_size = 1000)
        {
            _batchSize = batch_size;
            //_connectionString = $"Data Source={Path.Combine(folder, "db.sqlite")};Version=3;";        // got an exception that version isn't supported
            _connectionString = $"Data Source={Path.Combine(folder, "db.sqlite")}";
        }

        public void TruncateTables()
        {
            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                EnsureTableExists(connection);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"DELETE FROM CodeSnippet";
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Add this snippet to a table row
        /// </summary>
        /// <remarks>
        /// This isn't immediatly written to the db.  Calls are batched up to avoid heavy db io
        /// 
        /// Models.CodeFile has file/folder at parent and codesnippets as children, but the db just has extra columns
        /// for file/folder
        /// </remarks>
        public CodeSnippet AddSnippet(CodeSnippet snippet, string folder, string file)
        {
            _token++;

            CodeSnippet retVal = snippet with { UniqueID = _token, };

            _pendingSnippets.Add(new PendingSnippet()
            {
                Folder = folder,
                File = file,
                Snippet = retVal,
            });

            if (_pendingSnippets.Count >= _batchSize)
                FlushPending();

            return retVal;
        }

        // NOTE: this is slow.  INSERT INTO would be faster, but doesn't support parameterized queries
        public void FlushPending()
        {
            if (_pendingSnippets.Count == 0)
                return;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                EnsureTableExists(connection);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
@"INSERT INTO CodeSnippet(UniqueID, Folder, File, LineFrom, LineTo, NameSpace, ParentName, Inheritance, Name, Type, Text, Text_NoComments)
VALUES (@UniqueID, @Folder, @File, @LineFrom, @LineTo, @NameSpace, @ParentName, @Inheritance, @Name, @Type, @Text, @Text_NoComments);";

                    foreach (var snippet in _pendingSnippets)
                    {
                        command.Parameters.Clear();

                        command.Parameters.AddWithValue("@UniqueID", snippet.Snippet.UniqueID);
                        command.Parameters.AddWithValue("@Folder", snippet.Folder);
                        command.Parameters.AddWithValue("@File", snippet.File);
                        command.Parameters.AddWithValue("@LineFrom", snippet.Snippet.LineFrom);
                        command.Parameters.AddWithValue("@LineTo", snippet.Snippet.LineTo);
                        command.Parameters.AddWithValue("@NameSpace", snippet.Snippet.NameSpace ?? (object)DBNull.Value); // Handle nullable strings
                        command.Parameters.AddWithValue("@ParentName", snippet.Snippet.ParentName ?? (object)DBNull.Value); // Handle nullable strings
                        command.Parameters.AddWithValue("@Inheritance", snippet.Snippet.Inheritance ?? (object)DBNull.Value); // Handle nullable strings
                        command.Parameters.AddWithValue("@Name", snippet.Snippet.Name);
                        command.Parameters.AddWithValue("@Type", snippet.Snippet.Type);
                        command.Parameters.AddWithValue("@Text", snippet.Snippet.Text);
                        command.Parameters.AddWithValue("@Text_NoComments", snippet.Snippet.Text_NoComments);

                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        #region Private Methods

        private static void EnsureTableExists(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
@"CREATE TABLE IF NOT EXISTS CodeSnippet(
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UniqueID INTEGER,
    Folder TEXT,
    File TEXT,
    LineFrom INTEGER,
    LineTo INTEGER,
    NameSpace TEXT,
    ParentName TEXT,
    Inheritance TEXT,
    Name TEXT,
    Type TEXT,
    Text TEXT,
    Text_NoComments TEXT);

CREATE INDEX IF NOT EXISTS idx_uniqueid ON CodeSnippet (UniqueID);
CREATE INDEX IF NOT EXISTS idx_namespace ON CodeSnippet (NameSpace);
CREATE INDEX IF NOT EXISTS idx_parentname ON CodeSnippet (ParentName);
CREATE INDEX IF NOT EXISTS idx_name ON CodeSnippet (Name);
CREATE INDEX IF NOT EXISTS idx_type ON CodeSnippet (Type);
";

                command.ExecuteNonQuery();
            }
        }

        #endregion
    }
}
