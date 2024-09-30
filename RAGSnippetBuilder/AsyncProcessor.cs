using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    /// <summary>
    /// This takes work and does it in separate task
    /// </summary>
    /// <remarks>
    /// Instantiate this, then call ProcessAsync for each item that should be worked
    /// 
    /// It's safe to do a Task.WaitAll on a set of calls to ProcessAsync (won't deadlock the actual worker)
    /// </remarks>
    public class AsyncProcessor<TInput, TOutput>
    {
        private readonly ConcurrentQueue<(TInput Input, TaskCompletionSource<TOutput> Tcs)> _queue = new ConcurrentQueue<(TInput Input, TaskCompletionSource<TOutput> Tcs)>();
        private readonly Func<TInput, Task<TOutput>> _processor;
        private readonly int _maxConcurrency;

        public AsyncProcessor(Func<TInput, Task<TOutput>> processor, int maxConcurrency)
        {
            _processor = processor;
            _maxConcurrency = maxConcurrency;
            StartConsumers();
        }

        private void StartConsumers()
        {
            for (int i = 0; i < _maxConcurrency; i++)
            {
                Task.Run(async () =>
                {
                    while (true)
                    {
                        if (_queue.TryDequeue(out var item))
                        {
                            try
                            {
                                var result = await _processor(item.Input);
                                item.Tcs.SetResult(result);
                            }
                            catch (Exception ex)
                            {
                                item.Tcs.SetException(ex);
                            }
                        }
                        else
                        {
                            await Task.Delay(100); // No items in the queue, wait a bit before checking again
                        }
                    }
                });
            }
        }

        public Task<TOutput> ProcessAsync(TInput input)
        {
            var tcs = new TaskCompletionSource<TOutput>();
            _queue.Enqueue((input, tcs));
            return tcs.Task;
        }
    }
}
