﻿using System;
using System.Reflection.Emit;
#if NET47 || NET471 || NET472
using System.Runtime.CompilerServices;
#endif
using System.Runtime.InteropServices;

namespace DBClientFiles.NET.Utils
{
    /// <summary>
    /// Provides fast reading and writing of generic structures to a memory location using IL emitted functions.
    /// </summary>
    /// /// <remarks>
    /// This class was shamelessly stolen from <a href="https://www.nuget.org/packages/SharedMemory">SharpMemory</a>.
    /// </remarks>
    public static class FastStructure
    {
        /// <summary>
        /// Retrieve a pointer to the passed generic structure type. This is achieved by emitting a <see cref="DynamicMethod"/> to retrieve a pointer to the structure.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="structure"></param>
        /// <returns>A pointer to the provided structure in memory.</returns>
        /// <see cref="FastStructure{T}.GetPtr"/>]
#if NET47 || NET471 || NET472
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static unsafe void* GetPtr<T>(ref T structure)
            where T : struct
        {
            return FastStructure<T>.GetPtr(ref structure);
        }

        /// <summary>
        /// Loads the generic value type <typeparamref name="T"/> from a pointer. This is achieved by emitting a <see cref="DynamicMethod"/> that returns the value in the memory location as a <typeparamref name="T"/>.
        /// <para>The equivalent non-generic C# code:</para>
        /// <code>
        /// unsafe MyStruct ReadFromPointer(byte* pointer)
        /// {
        ///     return *(MyStruct*)pointer;
        /// }
        /// </code>
        /// </summary>
        /// <typeparam name="T">Any value/struct type</typeparam>
        /// <param name="pointer">Unsafe pointer to memory to load the value from</param>
        /// <returns>The newly loaded value</returns>
#if NET47
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static T PtrToStructure<T>(IntPtr pointer)
            where T : struct
        {
            return FastStructure<T>.PtrToStructure(pointer);
        }

        /// <summary>
        /// Writes the generic value type <typeparamref name="T"/> to the location specified by a pointer. This is achieved by emitting a <see cref="DynamicMethod"/> that copies the value from the referenced structure into the specified memory location.
        /// <para>There is no exact equivalent possible in C#, the closest possible (generates the same IL) is the following code:</para>
        /// <code>
        /// unsafe void WriteToPointer(ref Header dest, ref Header src)
        /// {
        ///     dest = src;
        /// }
        /// </code>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="pointer"></param>
        /// <param name="structure"></param>
#if NET47
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static void StructureToPtr<T>(ref T structure, IntPtr pointer)
            where T : struct
        {
            FastStructure<T>.StructureToPtr(ref structure, pointer);
        }

        /// <summary>
        /// Retrieve the cached size of a structure
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        /// <remarks>Caches the size by type</remarks>
        /// <see cref="FastStructure{T}.Size"/>
#if NET47
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        public static int SizeOf<T>()
            where T : struct
        {
            return FastStructure<T>.Size;
        }

        /// <summary>
        /// Reads a number of elements from a memory location into the provided buffer starting at the specified index.
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="buffer">The destination buffer.</param>
        /// <param name="source">The source memory location.</param>
        /// <param name="index">The start index within <paramref name="buffer"/>.</param>
        /// <param name="count">The number of elements to read.</param>
        public static unsafe void ReadArray<T>(T[] buffer, IntPtr source, int index, int count)
            where T : struct
        {
            var elementSize = (uint)SizeOf<T>();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (buffer.Length - index < count)
                throw new ArgumentException("Invalid offset into array specified by index and count");

            var ptr = source.ToPointer();
            var p = (byte*)GetPtr(ref buffer[0]);
            UnsafeNativeMethods.CopyMemoryPtr(p + (index * elementSize), ptr, (uint)(elementSize * count));
        }

        /// <summary>
        /// Writes a number of elements to a memory location from the provided buffer starting at the specified index.
        /// </summary>
        /// <typeparam name="T">The structure type</typeparam>
        /// <param name="destination">The destination memory location.</param>
        /// <param name="buffer">The source buffer.</param>
        /// <param name="index">The start index within <paramref name="buffer"/>.</param>
        /// <param name="count">The number of elements to write.</param>
        public static unsafe void WriteArray<T>(IntPtr destination, T[] buffer, int index, int count)
            where T : struct
        {
            var elementSize = (uint)SizeOf<T>();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));
            if (buffer.Length - index < count)
                throw new ArgumentException("Invalid offset into array specified by index and count");

            var ptr = destination.ToPointer();
            var p = (byte*)GetPtr(ref buffer[0]);
            UnsafeNativeMethods.CopyMemoryPtr(ptr, p + (index * elementSize), (uint)(elementSize * count));
        }
    }

    /// <summary>
    /// Emits optimized IL for the reading and writing of structures to/from memory.
    /// <para>For a 32-byte structure with 1 million iterations:</para>
    /// <para>The <see cref="FastStructure{T}.PtrToStructure"/> method performs approx. 20x faster than
    /// <see cref="System.Runtime.InteropServices.Marshal.PtrToStructure(IntPtr, Type)"/> (8ms vs 160ms), and about 1.6x slower than the non-generic equivalent (8ms vs 5ms)</para>
    /// <para>The <see cref="FastStructure{T}.StructureToPtr"/> method performs approx. 8x faster than 
    /// <see cref="System.Runtime.InteropServices.Marshal.StructureToPtr(object, IntPtr, bool)"/> (4ms vs 34ms). </para>
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public static unsafe class FastStructure<T>
        where T : struct
    {
        /// <summary>
        /// Delegate that returns a pointer to the provided structure. Use with extreme caution.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public unsafe delegate void* GetPtrDelegate(ref T value);

        /// <summary>
        /// Delegate for loading a structure from the specified memory address
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        public delegate T PtrToStructureDelegate(IntPtr pointer);

        /// <summary>
        /// Delegate for writing a structure to the specified meemory address
        /// </summary>
        /// <param name="value"></param>
        /// <param name="pointer"></param>
        public delegate void StructureToPtrDelegate(ref T value, IntPtr pointer);

        /// <summary>
        /// The <see cref="GetPtrDelegate"/> delegate for the generated IL to retrieve a pointer to the structure
        /// </summary>
        public static readonly GetPtrDelegate GetPtr = BuildFunction();

        /// <summary>
        /// The <see cref="PtrToStructureDelegate"/> delegate for the generated IL to retrieve a structure from a specified memory address.
        /// </summary>
        public static readonly PtrToStructureDelegate PtrToStructure = BuildLoadFromPointerFunction();

        /// <summary>
        /// The <see cref="StructureToPtrDelegate"/> delegate for the generated IL to store a structure at the specified memory address.
        /// </summary>
        public static readonly StructureToPtrDelegate StructureToPtr = BuildWriteToPointerFunction();

        /// <summary>
        /// Cached size of T as determined by <see cref="Marshal.SizeOf(Type)"/>.
        /// </summary>
#if NET45 || NETCOREAPP2_0 || NETCOREAPP2_1
        public static readonly int Size = Marshal.SizeOf(typeof(T));
#else
        public static readonly int Size = Marshal.SizeOf<T>();
#endif

        private static DynamicMethod method;
        private static DynamicMethod methodLoad;
        private static DynamicMethod methodWrite;

        private static GetPtrDelegate BuildFunction()
        {
            method = new DynamicMethod("GetStructurePtr<" + typeof(T).FullName + ">",
                typeof(void*), new[] { typeof(T).MakeByRefType() }, typeof(FastStructure).Module);

            var generator = method.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Conv_U);
            generator.Emit(OpCodes.Ret);
            return (GetPtrDelegate)method.CreateDelegate(typeof(GetPtrDelegate));
        }

        private static PtrToStructureDelegate BuildLoadFromPointerFunction()
        {
            methodLoad = new DynamicMethod("PtrToStructure<" + typeof(T).FullName + ">",
                typeof(T), new[] { typeof(IntPtr) }, typeof(FastStructure).Module);

            var generator = methodLoad.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Ret);

            return (PtrToStructureDelegate)methodLoad.CreateDelegate(typeof(PtrToStructureDelegate));
        }

        private static StructureToPtrDelegate BuildWriteToPointerFunction()
        {
            methodWrite = new DynamicMethod("StructureToPtr<" + typeof(T).FullName + ">",
                null, new[] { typeof(T).MakeByRefType(), typeof(IntPtr) }, typeof(FastStructure).Module);

            var generator = methodWrite.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldobj, typeof(T));
            generator.Emit(OpCodes.Stobj, typeof(T));
            generator.Emit(OpCodes.Ret);
            return (StructureToPtrDelegate)methodWrite.CreateDelegate(typeof(StructureToPtrDelegate));
        }
    }
}
