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

        private readonly object _lock = new object();
        private double _sum_seconds = 0;
        private int _num_calls = 0;

        public AsyncProcessor(Func<Func<TInput, Task<TOutput>>> serviceFactory, int maxConcurrency)
        {
            _serviceFactory = serviceFactory;
            _maxConcurrency = maxConcurrency;
            StartConsumers();
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

        public double AverageCallTime_Milliseconds
        {
            get
            {
                lock(_lock)
                {
                    return _sum_seconds / _num_calls * 1000;
                }
            }
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
                                DateTime now = DateTime.UtcNow;

                                var result = await service(item.Input);     // Process the input asynchronously using the service object

                                AddCallTime((DateTime.UtcNow - now).TotalSeconds);

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

        private void AddCallTime(double seconds)
        {
            lock(_lock)
            {
                _sum_seconds += seconds;
                _num_calls++;
            }
        }
    }
}
