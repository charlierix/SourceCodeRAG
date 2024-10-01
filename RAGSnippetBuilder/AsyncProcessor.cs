using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAGSnippetBuilder
{
    public class AsyncProcessor<TInput, TOutput>
    {
        private record WorkItem
        {
            public TInput Input { get; init; }
            public TaskCompletionSource<TOutput> Tcs { get; init; }
        }

        private readonly ConcurrentQueue<WorkItem> _queue = new ConcurrentQueue<WorkItem>();
        private readonly Func<Func<TInput, Task<TOutput>>> _serviceFactory;
        private readonly int _maxConcurrency;

        public AsyncProcessor(Func<Func<TInput, Task<TOutput>>> serviceFactory, int maxConcurrency)
        {
            _serviceFactory = serviceFactory;
            _maxConcurrency = maxConcurrency;
            StartConsumers();
        }

        private void StartConsumers()
        {
            for (int i = 0; i < _maxConcurrency; i++)
            {
                Task.Run(async () =>
                {
                    var service = _serviceFactory();    // Create a new instance of the service object for each consumer task

                    while (true)
                    {
                        if (_queue.TryDequeue(out var item))
                        {
                            try
                            {
                                var result = await service(item.Input);     // Process the input asynchronously using the service object
                                item.Tcs.SetResult(result);
                            }
                            catch (Exception ex)
                            {
                                item.Tcs.SetException(ex);
                            }
                        }
                        else
                        {
                            await Task.Yield();     // No items in the queue, yield to other tasks
                        }
                    }
                });
            }
        }

        public Task<TOutput> ProcessAsync(TInput input)
        {
            var workitem = new WorkItem()
            {
                Input = input,
                Tcs = new TaskCompletionSource<TOutput>(),
            };

            _queue.Enqueue(workitem);

            return workitem.Tcs.Task;
        }
    }
}
