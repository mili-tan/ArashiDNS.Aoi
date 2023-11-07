using Microsoft.Extensions.ObjectPool;
using System.Net;
using ARSoft.Tools.Net.Dns;
using System.Collections.Concurrent;
using System;

namespace Arashi.Aoi
{
    public class ObjectPool<T>(Func<T> objectGenerator)
    {
        private readonly ConcurrentBag<T> _objects = new ConcurrentBag<T>();
        private readonly Func<T> _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));

        public T Get() => _objects.TryTake(out T item) ? item : _objectGenerator();

        public void Return(T item) => _objects.Add(item);
    }
}
