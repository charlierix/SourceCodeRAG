using Microsoft.Data.Sqlite;
using RAGSnippetBuilder.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder.DAL
{
    public class DAL_SQLDB
    {
        #region record: PendingSnippet

        public record PendingSnippet
        {
            public string Folder { get; init; }
            public string File { get; init; }
            public CodeSnippet Snippet { get; init; }
        }

        #endregion
        #region record: PendingTag

        public record PendingTag
        {
            public long UniqueID { get; init; }
            public string Tag { get; init; }
        }

        #endregion

        #region Declaration Section

        private readonly string _connectionString;

        private readonly int _batchSize;
        private readonly List<PendingSnippet> _pendingSnippets = new List<PendingSnippet>();
        private readonly List<PendingTag> _pendingTags = new List<PendingTag>();

        #endregion

        public DAL_SQLDB(string folder, int batch_size = 10000)
        {
            _batchSize = batch_size;
            //_connectionString = $"Data Source={Path.Combine(folder, "sql.sqlite")};Version=3;";        // got an exception that version isn't supported
            _connectionString = $"Data Source={Path.Combine(folder, "sql.sqlite")}";
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
        public void AddSnippet(CodeSnippet snippet, string folder, string file)
        {
            AddSnippet(new PendingSnippet()
            {
                Folder = folder,
                File = file,
                Snippet = snippet,
            });
        }
        public void AddSnippet(PendingSnippet snippet)
        {
            _pendingSnippets.Add(snippet);

            if (_pendingSnippets.Count >= _batchSize)
                FlushPending_Snippets();
        }

        public void AddTags(CodeTags tags)
        {
            foreach (var tag in tags.Tags.Select(o => new PendingTag() { UniqueID = tags.UniqueID, Tag = o }))
                AddTag(tag);
        }
        public void AddTag(PendingTag tag)
        {
            _pendingTags.Add(tag);

            if (_pendingTags.Count >= _batchSize)
                FlushPending_Tags();
        }

        // NOTE: this is slow.  INSERT INTO would be faster, but doesn't support parameterized queries
        public void FlushPending_Snippets()
        {
            if (_pendingSnippets.Count == 0)
                return;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                EnsureTableExists(connection);

                // https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/bulk-insert
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText =
    @"INSERT INTO CodeSnippet(UniqueID, ParentID, Folder, File, LineFrom, LineTo, NameSpace, ParentName, Inheritance, Name, Type, Text)
VALUES (@UniqueID, @ParentID, @Folder, @File, @LineFrom, @LineTo, @NameSpace, @ParentName, @Inheritance, @Name, @Type, @Text);";

                        var uniqueID = command.CreateParameter();
                        uniqueID.ParameterName = "@UniqueID";
                        command.Parameters.Add(uniqueID);

                        var parentID = command.CreateParameter();
                        parentID.ParameterName = "@ParentID";
                        command.Parameters.Add(parentID);

                        var folder = command.CreateParameter();
                        folder.ParameterName = "@Folder";
                        command.Parameters.Add(folder);

                        var file = command.CreateParameter();
                        file.ParameterName = "@File";
                        command.Parameters.Add(file);

                        var lineFrom = command.CreateParameter();
                        lineFrom.ParameterName = "@LineFrom";
                        command.Parameters.Add(lineFrom);

                        var lineTo = command.CreateParameter();
                        lineTo.ParameterName = "@LineTo";
                        command.Parameters.Add(lineTo);

                        var ns = command.CreateParameter();
                        ns.ParameterName = "@NameSpace";
                        command.Parameters.Add(ns);

                        var parentName = command.CreateParameter();
                        parentName.ParameterName = "@ParentName";
                        command.Parameters.Add(parentName);

                        var inherit = command.CreateParameter();
                        inherit.ParameterName = "@Inheritance";
                        command.Parameters.Add(inherit);

                        var name = command.CreateParameter();
                        name.ParameterName = "@Name";
                        command.Parameters.Add(name);

                        var type = command.CreateParameter();
                        type.ParameterName = "@Type";
                        command.Parameters.Add(type);

                        var text = command.CreateParameter();
                        text.ParameterName = "@Text";
                        command.Parameters.Add(text);

                        foreach (var snippet in _pendingSnippets)
                        {
                            uniqueID.Value = snippet.Snippet.UniqueID;
                            parentID.Value = snippet.Snippet.ParentID;
                            folder.Value = snippet.Folder;
                            file.Value = snippet.File;
                            lineFrom.Value = snippet.Snippet.LineFrom;
                            lineTo.Value = snippet.Snippet.LineTo;
                            ns.Value = snippet.Snippet.NameSpace ?? (object)DBNull.Value; // Handle nullable strings
                            parentName.Value = snippet.Snippet.ParentName ?? (object)DBNull.Value; // Handle nullable strings
                            inherit.Value = snippet.Snippet.Inheritance ?? (object)DBNull.Value; // Handle nullable strings
                            name.Value = snippet.Snippet.Name;
                            type.Value = snippet.Snippet.Type;
                            text.Value = snippet.Snippet.Text;

                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }

            _pendingSnippets.Clear();
        }
        public void FlushPending_Tags()
        {
            if (_pendingTags.Count == 0)
                return;

            using (var connection = new SqliteConnection(_connectionString))
            {
                connection.Open();

                EnsureTableExists(connection);

                // https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/bulk-insert
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText =
    @"INSERT INTO CodeTags(UniqueID, Tag)
VALUES (@UniqueID, @Tag);";

                        var uniqueID = command.CreateParameter();
                        uniqueID.ParameterName = "@UniqueID";
                        command.Parameters.Add(uniqueID);

                        var tag = command.CreateParameter();
                        tag.ParameterName = "@Tag";
                        command.Parameters.Add(tag);

                        foreach (var item in _pendingTags) 
                        {
                            uniqueID.Value = item.UniqueID;
                            tag.Value = item.Tag;

                            command.ExecuteNonQuery();
                        }
                    }

                    transaction.Commit();
                }
            }

            _pendingTags.Clear();
        }

        #region Private Methods

        private static void EnsureTableExists(SqliteConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
@"CREATE TABLE IF NOT EXISTS CodeSnippet(
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    UniqueID INTEGER NOT NULL UNIQUE,
    ParentID INTEGER,
    Folder NOT NULL TEXT,
    File NOT NULL TEXT,
    LineFrom NOT NULL INTEGER,
    LineTo NOT NULL INTEGER,
    NameSpace TEXT,
    ParentName TEXT,
    Inheritance TEXT,
    Name TEXT,
    Type NOT NULL TEXT,
    Text TEXT);

CREATE INDEX IF NOT EXISTS idx_uniqueid ON CodeSnippet (UniqueID);
CREATE INDEX IF NOT EXISTS idx_namespace ON CodeSnippet (NameSpace);
CREATE INDEX IF NOT EXISTS idx_parentname ON CodeSnippet (ParentName);
CREATE INDEX IF NOT EXISTS idx_name ON CodeSnippet (Name);
CREATE INDEX IF NOT EXISTS idx_type ON CodeSnippet (Type);
";

                command.ExecuteNonQuery();


                command.CommandText =
@"CREATE TABLE IF NOT EXISTS CodeTags(
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    UniqueID NOT NULL INTEGER,
    Tag TEXT NOT NULL,
CONSTRAINT uq_uniqueid_tag UNIQUE (UniqueID, Tag));
";

                // living dangerously and not adding the constraint.  If that were added, snippet and tags would all have to be inserted in the same transaction.  Since this is a one shot process that populates the databases, everything should be there by the end
                //FOREIGN KEY (UniqueID) REFERENCES CodeSnippet(UniqueID),  -- Foreign key constraint to Snippet table

                command.ExecuteNonQuery();
            }
        }

        #endregion
    }
}
