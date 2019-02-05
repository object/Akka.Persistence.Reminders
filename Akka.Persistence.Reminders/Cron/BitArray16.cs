#region copyright
// -----------------------------------------------------------------------
//  <copyright file="BitArray16.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2019 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Akka.Persistence.Reminders.Cron
{
    internal ref struct BitArray16
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator ushort(BitArray16 array) => array._value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BitArray32(in BitArray16 array) => new BitArray32((uint)array._value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BitArray8(in BitArray16 array) => new BitArray8((byte)array._value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator BitArray64(in BitArray16 array) => new BitArray64((ulong)array._value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in BitArray16 x, in BitArray16 y) => x._value == y._value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in BitArray16 x, in BitArray16 y) => x._value != y._value;

        public const int Length = 16;
        private ushort _value;

        public BitArray16(ushort value)
        {
            _value = value;
        }

        public bool this[int index]
        {
            get => (_value & (ushort)(1 << index)) != 0;
            set
            {
                if (value)
                    _value |= (ushort)(1u << index);
                else
                    _value &= (ushort)(~(1 << index));
            }
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Length;
        }

        public bool IsReadOnly => false;

        public void Insert(int index, bool item)
        {
            if (index > Length)
                throw new ArgumentOutOfRangeException($"Index '{index}' is outside of the bounds of {nameof(BitArray16)}");

            this[index] = item;
        }

        public int IndexOf(bool item)
        {
            for (int i = 0; i < Length; i++)
            {
                if (this[i] == item) return i;
            }

            return -1;
        }

        public IEnumerator<bool> GetEnumerator()
        {
            for (int i = 0; i < Length; i++)
            {
                yield return this[i];
            }
        }
        
        public void Clear() => _value = 0;

        public bool Contains(bool item) => IndexOf(item) != -1;

        public void CopyTo(bool[] array, int arrayIndex)
        {
            for (int i = 0; i < Length; i++)
            {
                array[arrayIndex + i] = this[i];
            }
        }

        public BitArray16 Slice(int start)
        {
            unchecked
            {
                var trimStart = (_value << start) & ushort.MaxValue;
                var final = (trimStart >> start);
                return new BitArray16((ushort)final);
            }
        }

        public BitArray16 Slice(int start, int count)
        {
            unchecked
            {
                var end = start + count;
                var trimStart = (_value << start) & ushort.MaxValue;
                var trimEnd = (_value >> end) & ushort.MaxValue;
                var final = (trimStart >> start) & (trimEnd << end);
                return new BitArray16((ushort)final);
            }
        }

        public override bool Equals(object obj) => throw new Exception("Ref struct equality not supported");

        public override int GetHashCode() => throw new Exception("Ref struct hashcode not supported");
    }
}