﻿// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using fennecs.pools;

namespace fennecs;

/// <summary>
/// <para>
/// Query with 1 output Stream Type, <c>C0</c>.
/// </para>
/// <para>
/// Queries expose methods to rapidly iterate all Entities that match their Mask and Stream Types.
/// </para>
/// <ul>
/// <li><c>ForEach(...)</c> - call a delegate <see cref="ComponentAction{C0}"/> for each Entity.</li>
/// <li><c>Job(...)</c> - parallel process, calling a delegate <see cref="ComponentAction{C0}"/> for each Entity.</li>
/// <li><c>Raw(...)</c> - pass Memory regions / Spans to a delegate <see cref="MemoryAction{C0}"/> per matched Archetype (× matched Wildcards) of entities.</li>
/// </ul>
/// </summary>
/// <remarks>
/// 
/// </remarks>
public class Query<C0> : Query where C0 : notnull
{
    #region Internals

    /// <summary>
    /// Initializes a new instance of the <see cref="Query{C0}"/> class.
    /// </summary>
    /// <param name="world">The world context for the query.</param>
    /// <param name="streamTypes">The stream types for the query.</param>
    /// <param name="mask">The mask for the query.</param>
    /// <param name="archetypes">The archetypes for the query.</param>
    internal Query(World world, List<TypeExpression> streamTypes, Mask mask, List<Archetype> archetypes) : base(world, streamTypes, mask, archetypes)
    { }

    #endregion

    #region Runners

    /// <include file='XMLdoc.xml' path='members/member[@name="T:For"]'/>
    public void For(ComponentAction<C0> action)
    {
        using var worldLock = World.Lock();
        foreach (var table in Archetypes)
        {

            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            do
            {
                var s0 = join.Select;
                var span0 = s0.Span;
                // foreach is faster than for loop & unroll
                foreach (ref var c0 in span0) action(ref c0);
            } while (join.Iterate());
        }
    }


    // #region Showcase
    /// <summary>
    /// Executes an action for each entity that matches the query, passing an additional uniform parameter to the action.
    /// </summary>
    /// <param name="action"><see cref="UniformComponentAction{C0,U}"/> taking references to Component Types.</param>
    /// <param name="uniform">The uniform data to pass to the action.</param>
    // /// <include file='XMLdoc.xml' path='members/member[@name="T:ForU"]'/>
    public void For<U>(UniformComponentAction<C0, U> action, U uniform)
    {
        using var worldLock = World.Lock();

        foreach (var table in Archetypes)
        {

            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;
            do
            {
                var s0 = join.Select;
                var span0 = s0.Span;
                // foreach is faster than for loop & unroll
                foreach (ref var c0 in span0) action(ref c0, uniform);
            } while (join.Iterate());
        }
    }
    // #endregion Showcase


    /// <include file='XMLdoc.xml' path='members/member[@name="T:ForE"]'/>
    public void For(EntityComponentAction<C0> componentAction)
    {
        using var worldLock = World.Lock();
        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            var count = table.Count;
            do
            {
                var s0 = join.Select;
                var span0 = s0.Span;

                for (var i = 0; i < count; i++) componentAction(table[i], ref span0[i]);
            } while (join.Iterate());
        }
    }


    /// <include file='XMLdoc.xml' path='members/member[@name="T:ForEU"]'/>
    public void For<U>(UniformEntityComponentAction<C0, U> componentAction, U uniform)
    {
        using var worldLock = World.Lock();
        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            var count = table.Count;
            do
            {
                var s0 = join.Select;
                var span0 = s0.Span;

                for (var i = 0; i < count; i++) componentAction(table[i], ref span0[i], uniform);
            } while (join.Iterate());
        }
    }


    /// <summary>
    /// Executes an action <em>in parallel chunks</em> for each entity that matches the query.
    /// </summary>
    /// <param name="action"><see cref="ComponentAction{C0}"/> taking references to Component Types.</param>
    public void Job(ComponentAction<C0> action)
    {
        var chunkSize = Math.Max(1, Count / Concurrency);

        using var worldLock = World.Lock();
        Countdown.Reset();

        using var jobs = PooledList<Work<C0>>.Rent();

        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            var count = table.Count; // storage.Length is the capacity, not the count.
            var partitions = count / chunkSize + Math.Sign(count % chunkSize);
            do
            {
                for (var chunk = 0; chunk < partitions; chunk++)
                {
                    Countdown.AddCount();

                    var start = chunk * chunkSize;
                    var length = Math.Min(chunkSize, count - start);

                    var s0 = join.Select;

                    var job = JobPool<Work<C0>>.Rent();
                    job.Memory1 = s0.AsMemory(start, length);
                    job.Action = action;
                    job.CountDown = Countdown;
                    jobs.Add(job);

                    ThreadPool.UnsafeQueueUserWorkItem(job, true);
                }
            } while (join.Iterate());
        }

        Countdown.Signal();
        Countdown.Wait();

        JobPool<Work<C0>>.Return(jobs);
    }


    /// <summary>
    /// Executes an action <em>in parallel chunks</em> for each entity that matches the query, passing an additional uniform parameter to the action.
    /// </summary>
    /// <param name="action"><see cref="ComponentAction{C0}"/> taking references to Component Types.</param>
    /// <param name="uniform">The uniform data to pass to the action.</param>
    public void Job<U>(UniformComponentAction<C0, U> action, U uniform)
    {
        var chunkSize = Math.Max(1, Count / Concurrency);

        using var worldLock = World.Lock();
        Countdown.Reset();

        using var jobs = PooledList<UniformWork<C0, U>>.Rent();

        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);


            var count = table.Count; // storage.Length is the capacity, not the count.
            var partitions = count / chunkSize + Math.Sign(count % chunkSize);
            do
            {
                for (var chunk = 0; chunk < partitions; chunk++)
                {
                    Countdown.AddCount();

                    var start = chunk * chunkSize;
                    var length = Math.Min(chunkSize, count - start);

                    var s0 = join.Select;

                    var job = JobPool<UniformWork<C0, U>>.Rent();
                    job.Memory1 = s0.AsMemory(start, length);
                    job.Action = action;
                    job.Uniform = uniform;
                    job.CountDown = Countdown;
                    jobs.Add(job);

                    ThreadPool.UnsafeQueueUserWorkItem(job, true);
                }
            } while (join.Iterate());
        }

        Countdown.Signal();
        Countdown.Wait();

        JobPool<UniformWork<C0, U>>.Return(jobs);
    }


    /// <summary>
    /// Executes an action passing in bulk data in <see cref="Memory{T}"/> streams that match the query.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Suggested uses include search algorithms with early-out, and passing bulk data into a game engine's native structures.
    /// </para>
    /// <para>
    /// <see cref="Memory{T}"/> contains a <c>Span</c> that can be used to access the data in a contiguous block of memory.
    /// </para>
    /// </remarks>
    /// <param name="action"><see cref="MemoryAction{C0}"/> action to execute.</param>
    public void Raw(MemoryAction<C0> action)
    {
        using var worldLock = World.Lock();

        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            do
            {
                var s0 = join.Select;
                var mem0 = s0.AsMemory(0, table.Count);

                action(mem0);
            } while (join.Iterate());
        }
    }


    /// <summary>
    /// Executes an action passing in bulk data in <see cref="Memory{T}"/> streams that match the query, and providing an additional uniform parameter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Suggested uses include search algorithms with early-out, and passing bulk data into a game engine's native structures.
    /// </para>
    /// <para>
    /// <see cref="Memory{T}"/> contains a <c>Span</c> that can be used to access the data in a contiguous block of memory.
    /// </para>
    /// </remarks>
    /// <param name="uniformAction"><see cref="MemoryAction{C0}"/> action to execute.</param>
    /// <param name="uniform">The uniform data to pass to the action.</param>
    public void Raw<U>(MemoryUniformAction<C0, U> uniformAction, U uniform)
    {
        using var worldLock = World.Lock();

        foreach (var table in Archetypes)
        {
            using var join = table.CrossJoin<C0>(StreamTypes);
            if (join.Empty) continue;

            do
            {
                var s0 = join.Select;
                var mem0 = s0.AsMemory(0, table.Count);

                uniformAction(mem0, uniform);
            } while (join.Iterate());
        }
    }

    #endregion

    #region Blitters

    /// <summary>
    /// <para>Blit (write) a component value of a stream type to all entities matched by this query.</para>
    /// <para>🚀 Very fast!</para>
    /// </summary>
    /// <remarks>
    /// Each entity in the Query must possess the component type.
    /// Otherwise, consider using <see cref="Query.Add{T}()"/> with <see cref="Batch.AddConflict.Replace"/>. 
    /// </remarks>
    /// <param name="value">a component value</param>
    /// <param name="target">default for Plain components, Entity for Relations, Identity.Of(Object) for ObjectLinks </param>
    public void Blit(C0 value, Match target = default)
    {
        var typeExpression = TypeExpression.Of<C0>(target);

        foreach (var table in Archetypes)
        {
            table.Fill(typeExpression, value);
        }
    }

    #endregion

    #region Warmup & Unroll

    /// <inheritdoc />
    public override Query<C0> Warmup()
    {
        base.Warmup();

        C0 c = default!;
        NoOp(ref c);
        NoOp(ref c, 0);

        Job(NoOp);
        Job(NoOp, 0);

        return this;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NoOp(ref C0 c0)
    { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void NoOp(ref C0 c0, int uniform)
    { }

    #endregion
}
