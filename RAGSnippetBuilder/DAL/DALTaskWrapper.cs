using RAGSnippetBuilder.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RAGSnippetBuilder.DAL.DAL_SQLDB;

namespace RAGSnippetBuilder.DAL
{
    /// <summary>
    /// The DAL takes snippets, then pushes to db every thousand rows
    /// This puts the dal instance in its own task, interacts through a queue, and pauses the main thread when the queue
    /// gets too full
    /// </summary>
    /// <remarks>
    /// Hopefully this extra layer will allow other code to not hang as hard when the db is busy
    /// </remarks>
    public class DALTaskWrapper
    {
        private const int MAX_QUEUE = 1500;

        private readonly ConcurrentQueue<DAL_SQLDB.PendingSnippet> _queue_snippet = new ConcurrentQueue<DAL_SQLDB.PendingSnippet>();
        private readonly ConcurrentQueue<DAL_SQLDB.PendingTag> _queue_tag = new ConcurrentQueue<DAL_SQLDB.PendingTag>();
        private readonly ConcurrentQueue<string> _queue_finish = new ConcurrentQueue<string>();

        public DALTaskWrapper(string folder)
        {
            RunDAL(folder);
        }

        public void Add(CodeSnippet snippet, string folder, string file)
        {
            // Hold up this main thread if there are too many pending writes
            while (_queue_snippet.Count > MAX_QUEUE)
                Task.Delay(150).Wait();

            _queue_snippet.Enqueue(new DAL_SQLDB.PendingSnippet()
            {
                Folder = folder,
                File = file,
                Snippet = snippet,
            });
        }
        public void Add(CodeTags tags)
        {
            // Hold up this main thread if there are too many pending writes
            while (_queue_tag.Count > MAX_QUEUE)
                Task.Delay(150).Wait();

            foreach (var tag in tags.Tags.Select(o => new PendingTag() { UniqueID = tags.UniqueID, Tag = o }))
                _queue_tag.Enqueue(tag);
        }

        public void Finished()
        {
            // Wait until all previous adds are processed
            while (_queue_snippet.Count > 0)
                Task.Delay(150).Wait();

            while (_queue_tag.Count > 0)
                Task.Delay(150).Wait();

            // Tell the dal to flush
            _queue_finish.Enqueue("");

            // Wait until the flush has finished
            while (_queue_finish.Count > 0)
                Task.Delay(150).Wait();
        }

        private void RunDAL(string folder)
        {
            Task.Run(() =>
            {
                DAL_SQLDB dal = new DAL_SQLDB(folder);

                while (true)
                {
                    bool found_one = false;

                    if (_queue_snippet.TryDequeue(out var snippet))
                    {
                        dal.AddSnippet(snippet);
                        found_one = true;
                    }

                    if (_queue_tag.TryDequeue(out var tag))
                    {
                        dal.AddTag(tag);
                        found_one = true;
                    }

                    if (_queue_finish.TryDequeue(out _))
                    {
                        dal.FlushPending_Snippets();
                        dal.FlushPending_Tags();
                        break;
                    }

                    if (!found_one)
                        Task.Yield();     // No items in the queue, yield to other tasks
                }
            });
        }
    }
}
