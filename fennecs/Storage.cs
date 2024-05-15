﻿using System.Numerics;
using System.Reflection.Metadata.Ecma335;

namespace fennecs;

/// <summary>
/// Generic Storage Interface (with boxing).
/// </summary>
internal interface IStorage
{
    int Count { get; }

    /// <summary>
    /// Stores a boxed value at the given index.
    /// </summary>
    void Store(int index, object value);

    /// <summary>
    /// Adds a boxed value (or number of identical values) to the storage.
    /// </summary>
    void Append(object value, int additions = 1);

    /// <summary>
    /// Removes a range of elements. 
    /// </summary>
    void Delete(int index, int removals = 1);

    /// <summary>
    /// Writes the given boxed value over all elements of the storage.
    /// </summary>
    /// <param name="value"></param>
    void Blit(object value);

    /// <summary>
    /// Clears the entire storage.
    /// </summary>
    void Clear();

    /// <summary>
    /// Ensures the storage has the capacity to store at least the given number of elements.
    /// </summary>
    /// <param name="capacity">the desired minimum capacity</param>
    void EnsureCapacity(int capacity);

    /// <summary>
    /// Tries to downsize the storage to the smallest power of 2 that can contain all elements.
    /// </summary>
    void Compact();

    /// <summary>
    /// Moves all elements from this storage into destination.
    /// The destination must be the same or a derived type.
    /// </summary>
    /// <param name="destination">a storage of the type of this storage</param>
    void Migrate(IStorage destination);

    /// <summary>
    /// Moves one element from this storage to the destination storage. 
    /// </summary>
    /// <param name="index">element index to move</param>
    /// <param name="destination">a storage of the same type</param>
    void Move(int index, IStorage destination);

    /// <summary>
    /// Instantiates the appropriate Storage for a <see cref="TypeExpression"/>.
    /// </summary>
    /// <param name="expression">a typeexpression</param>
    /// <returns>generic IStorage reference backed by the specialized instance of the <see cref="Storage{T}"/></returns>
    public static IStorage Instantiate(TypeExpression expression)
    {
        var storageType = typeof(Storage<>).MakeGenericType(expression.Type);
        var instance = (IStorage)Activator.CreateInstance(storageType)!;
        if (instance == null) throw new InvalidOperationException($"Could not instantiate Storage for {expression}");
        return instance;
    }
}

/// <summary>
/// A front-end to System.Array for fast storage write and blit operations.
/// </summary>
/// <typeparam name="T">the type of the array elements</typeparam>
internal class Storage<T> : IStorage
{
    private const int initialCapacity = 2;
        
    private T[] _data = new T[initialCapacity];

    /// <summary>
    ///  Stores a value at the given index.
    /// </summary>
    public void Store(int index, T value)
    {
        _data[index] = value;
        if (index > Count) Count = index; //HACK - must refactor to use Add (and less store)
    }


    /// <inheritdoc />
    public void Store(int index, object value) => Store(index, (T)value);


    /// <summary>
    /// Number of Elements actually stored.
    /// </summary>
    public int Count { get; private set; }


    /// <summary>
    /// Number of Elements actually stored.
    /// </summary>
    public int Capacity => _data.Length;


    /// <summary>
    /// Adds a value (or number of identical values) to the storage.
    /// </summary>
    public void Append(T value, int additions = 1)
    {
        if (additions <= 0) return;
        EnsureCapacity(Count + additions);
        FullSpan.Slice(Count, additions).Fill(value);
        Count += additions;
    }


    /// <summary>
    /// Adds a boxed value (or number of identical values) to the storage.
    /// </summary>
    public void Append(object value, int additions = 1) => Append((T)value, additions);


    /// <summary>
    /// Removes a range of items from the storage.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="removals"></param>
    public void Delete(int index, int removals = 1)
    {
        if (removals <= 0) return;

        // Are there enough elements after the removal site to fill the gap created?
        if (Count - removals > index + removals)
        {
            // Then copy just these elements to the site of removal!
            FullSpan[(Count - removals)..Count].CopyTo(FullSpan[index..]);
        }
        else
        {
            // Else shift back all remaining elements.
            FullSpan[(index + removals)..Count].CopyTo(FullSpan[index..]);
        }

        // Clear the space at the end.
        FullSpan[(Count - removals)..].Clear();
        
        Count -= removals;

        /*
        // Naive Wasteful: Shift EVERYTHING backwards.
        FullSpan[(index + removals)..].CopyTo(FullSpan[index..]);
        if (Count < _data.Length) FullSpan[Count..].Clear();
        */        
    }


    /// <summary>
    /// Writes the given value over all elements of the storage.
    /// </summary>
    /// <param name="value"></param>
    public void Blit(T value)
    {
        Span[..Count].Fill(value);
    }


    /// <summary>
    /// Writes the given boxed value over all elements of the storage.
    /// </summary>
    /// <param name="value"></param>
    public void Blit(object value) => Blit((T)value);


    /// <summary>
    /// Clears the entire storage.
    /// </summary>
    public void Clear()
    {
        Span.Clear();
        Count = 0;
    }

    /// <summary>
    /// Ensures the storage has the capacity to store at least the given number of elements.
    /// </summary>
    /// <param name="capacity">the desired minimum capacity</param>
    public void EnsureCapacity(int capacity)
    {
        var newSize = (int)BitOperations.RoundUpToPowerOf2((uint)capacity);
        if (newSize <= _data.Length) return;
        Array.Resize(ref _data, newSize);
    }

    /// <summary>
    /// Tries to downsize the storage to the smallest power of 2 that can contain all elements.
    /// </summary>
    public void Compact()
    {
        var newSize = (int)BitOperations.RoundUpToPowerOf2((uint)Math.Max(initialCapacity, Count));
        Array.Resize(ref _data, newSize);
    }


    /// <summary>
    /// Migrates all the entries in this storage to the destination storage.
    /// </summary>
    /// <param name="destination">a storage of the same type</param>
    public void Migrate(Storage<T> destination)
    {
        if (destination.Count >= Count)
        {
            destination.Append(Span);
        }
        else
        {
            // In many cases, we're migrating a much larger Archetype/Storage into a smaller or empty one.
            Append(destination.Span);

            // the old switcheroo 🦊
            (_data, destination._data) = (destination._data, _data);
        }

        // We are still the "source" archetype, so we are expected to be empty (and we do the emptying)
        Clear();
    }

    /// <summary>
    /// Moves one element from this storage to the destination storage.
    /// </summary>
    /// <param name="index">element index to move</param>
    /// <param name="destination">a storage of the same type</param>
    public void Move(int index, Storage<T> destination)
    {
        destination.Append(Span[index]);
        Delete(index);
    }

    /// <inheritdoc/>
    public void Move(int index, IStorage destination) => Move(index, (Storage<T>)destination);

    /// <summary>
    /// Boxed / General migration method.
    /// </summary>
    /// <param name="destination">a storage, must be of the same type</param>
    public void Migrate(IStorage destination) => Migrate((Storage<T>)destination);


    private void Append(Span<T> appendage)
    {
        EnsureCapacity(Count + appendage.Length);
        appendage.CopyTo(FullSpan[Count..]);
        Count += appendage.Length;
    }

    /// <summary>
    /// A wrapping copy implementation to nicely fill storages with data.
    /// </summary>
    /// <param name="values"></param>
    /// <param name="wrap"></param>
    /// <exception cref="ArgumentException"></exception>
    public void Blit(Span<T> values, bool wrap = false)
    {
        if (values.Length == 0) throw new ArgumentException("Cannot Blit empty Span.");

        for (var i = 0; i < Count - values.Length; i += values.Length)
        {
            values.CopyTo(Span[i..]);
        }

        for (var i = Count - Count % values.Length; i < Count; i++)
        {
            Span[i] = values[i % values.Length];
        }
    }

    public Memory<T> AsMemory(int start, int length)
    {
        return _data.AsMemory(start, length);
    }

    /// <summary>
    /// Returns a span representation of the actually contained data.
    /// </summary>
    public Span<T> Span => _data.AsSpan(0, Count);

    private Span<T> FullSpan => _data.AsSpan();

    /// <summary>
    /// Indexer (for debug purposes!)
    /// </summary>
    /// <remarks>
    /// Allows inspection of the entire array, not just the used elements.
    /// </remarks>
    internal T this[int index] => _data[index];
}