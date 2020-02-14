using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace IteratingTests
{
    public class Tests
    {
        private readonly IEnumerable<string> _thingsToDo = Enumerable.Range(0, 5).Select(x => x.ToString());

        private IAsyncEnumerable<string> _thingsToDoAsync
        {
            get
            {
                return RangeAsync();
            }
        }

        async IAsyncEnumerable<string> RangeAsync()
        {
            foreach (var item in _thingsToDo)
            {
                await Task.Delay(1000);
                yield return item;
            }
        }

        private readonly Stopwatch _sw = new Stopwatch();

        private List<string> _result;

        [SetUp]
        public void Setup()
        {
            _sw.Start();
            _result = new List<string>();
        }

        [TearDown]
        public void End()
        {
            Console.WriteLine(string.Join(',', _result));
            _sw.Stop();
            Console.WriteLine($"Done in {_sw.ElapsedMilliseconds}");
            _sw.Reset();
        }

        [Test]
        public async Task UsualForeach()
        {
            foreach (var thing in _thingsToDo)
            {
                _result.Add(await DoWork(thing));
            }
            Assert.AreEqual(_thingsToDo.Count(), _result.Count);
        }

        [Test]
        public async Task ForeachWithoutSugar()
        {
            IEnumerator<string> e = _thingsToDo.GetEnumerator();
            try
            {
                while (e.MoveNext())
                {
                    _result.Add(await DoWork(e.Current));;
                }
            }
            finally
            {
                e.Dispose();
            }
            Assert.AreEqual(_thingsToDo.Count(), _result.Count);
        }

        [Test]
        public void PLINQForeach()
        {
            Parallel.ForEach(_thingsToDo, item =>
            {
                var res = DoWork(item).GetAwaiter().GetResult();
                lock (this)
                {
                    _result.Add(res);
                }
            });
            Assert.AreEqual(_thingsToDo.Count(), _result.Count);
        }

        [Test]
        public void PLINQForeachWithMaxDegree()
        {
            Console.WriteLine($"CPU counts {Environment.ProcessorCount}");
            var options = new ParallelOptions { MaxDegreeOfParallelism = 2 };
            Parallel.ForEach(_thingsToDo, options, item =>
            {
                var res = DoWork(item).GetAwaiter().GetResult();
                lock (this)
                {
                    _result.Add(res);
                }
            });
            Assert.AreEqual(_thingsToDo.Count(), _result.Count);
        }

        [Test]
        public async Task TaskWhenAll()
        {
            var tasks = new List<Task<string>>();
            foreach (var thing in _thingsToDo)
            {
                tasks.Add(DoWork(thing));
            }
            _result = (await Task.WhenAll(tasks)).ToList();
            Assert.AreEqual(_thingsToDo.Count(), _result.Count);
        }

        [Test]
        public async Task TaskWhenAllWithMaxDegree()
        {
            var tasks = new List<Task<string>>();

            SemaphoreSlim semaphore = new SemaphoreSlim(2, 2);

            foreach (var thing in _thingsToDo)
            {
                Func<Task<string>> f = async () =>
                {
                    try
                    {
                        await semaphore.WaitAsync();
                        return await DoWork(thing);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                };

                tasks.Add(f.Invoke());
            }
            _result = (await Task.WhenAll(tasks)).ToList();
            Assert.AreEqual(_thingsToDo.Count(), _result.Count);
        }

        [Test]
        public async Task AsyncForeach()
        {
            await foreach (var thing in _thingsToDoAsync)
            {
                _result.Add(await DoWork(thing));
            }
            Assert.AreEqual(_thingsToDo.Count(), _result.Count);
        }

        private async Task<string> DoWork(string item)
        {
            Console.WriteLine($"{_sw.ElapsedMilliseconds} item {item} started");
            await Task.Delay(1000);
            Console.WriteLine($"{_sw.ElapsedMilliseconds} item {item} ended");
            return item;
        }
    }
}