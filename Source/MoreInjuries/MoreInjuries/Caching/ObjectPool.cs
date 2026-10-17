namespace MoreInjuries.Caching;

/// <summary>
/// A pool that attempts to reuse objects, allocating new ones only when the pool is empty.
/// </summary>
/// <remarks>
/// Gameplay callers are main-thread only. This pool is a fixed-capacity stack and does not take a lock.
/// </remarks>
/// <typeparam name="T">The type of the object to pool.</typeparam>
/// <remarks>
/// Initializes a new instance of the <see cref="ObjectPool{T}"/> class.
/// </remarks>
/// <param name="maxCapacity">The maximum number of objects to pool.</param>
/// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCapacity"/> is less than or equal to zero.</exception>
public sealed class ObjectPool<T>(int maxCapacity, Func<IPool<T>, T> factory) : IPool<T>, IDisposable where T : class
{
    private readonly T?[] _pool = new T[maxCapacity];
    private int _index;
    private bool _disposedValue;

    /// <inheritdoc/>
    public int MaxCapacity => _pool.Length;

    /// <inheritdoc/>
    public int Count => _index;

    /// <inheritdoc/>
    public T Rent()
    {
        if (_index == 0)
        {
            return factory.Invoke(this);
        }
        _index--;
        T element = _pool[_index]!;
        _pool[_index] = null;
        return element;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// This method does not guarantee that the object will be successfully returned to the pool, even if the pool is not full.
    /// </remarks>
    public bool Return(T item)
    {
        if (_index < _pool.Length)
        {
            _pool[_index] = item;
            _index++;
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposedValue)
        {
            return;
        }
        for (int i = 0; i < _pool.Length; i++)
        {
            if (_pool[i] is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        _disposedValue = true;
    }
}
