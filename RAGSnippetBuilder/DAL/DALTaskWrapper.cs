using RAGSnippetBuilder.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private readonly ConcurrentQueue<DAL_SQLDB.PendingSnippet> _queue = new ConcurrentQueue<DAL_SQLDB.PendingSnippet>();
        private readonly ConcurrentQueue<string> _queue_finish = new ConcurrentQueue<string>();

        public DALTaskWrapper(string folder)
        {
            RunDAL(folder);
        }

        public void Add(CodeSnippet snippet, string folder, string file)
        {
            // Hold up this main thread if there are too many pending writes
            while(_queue.Count > 1500)
                Task.Delay(150).Wait();

            _queue.Enqueue(new DAL_SQLDB.PendingSnippet()
            {
                Folder = folder,
                File = file,
                Snippet = snippet,
            });
        }

        public void Finished()
        {
            // Wait until all previous adds are processed
            while (_queue.Count > 0)
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
                    if (_queue.TryDequeue(out var item))
                    {
                        dal.AddSnippet(item);
                    }
                    else
                    {
                        if (_queue_finish.TryDequeue(out _))
                        {
                            dal.FlushPending();
                            break;
                        }
                        else
                        {
                            Task.Yield();     // No items in the queue, yield to other tasks
                        }
                    }
                }
            });
        }
    }
}
