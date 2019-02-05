#region copyright
// -----------------------------------------------------------------------
//  <copyright file="Utils.cs" creator="Bartosz Sypytkowski">
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
    internal struct BitArray8 : IList<bool>
    {
        private const int Length = 8;
        private byte _value;

        public BitArray8(byte value)
        {
            _value = value;
        }

        public bool this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (_value & (byte) (1 << index)) != 0;

            set
            {
                if (value)
                    _value |= (byte) (1 << index);
                else
                    _value &= (byte) (~(1 << index));
            }
        }

        public int Count => Length;
        public bool IsReadOnly => false;

        public void Insert(int index, bool item)
        {
            if (index > Length)
                throw new ArgumentOutOfRangeException($"Index '{index}' is outside of the bounds of {nameof(BitArray8)}");

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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Clear() => _value = 0;

        public bool Contains(bool item) => IndexOf(item) != -1;

        public void CopyTo(bool[] array, int arrayIndex)
        {
            for (int i = 0; i < Length; i++)
            {
                array[arrayIndex + i] = this[i];
            }
        }

        public void Add(bool item)
        {
            throw new InvalidOperationException($"{nameof(BitArray8)} doesn't support add operation");
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException($"{nameof(BitArray8)} doesn't support remove operation");
        }

        public bool Remove(bool item)
        {
            throw new InvalidOperationException($"{nameof(BitArray8)} doesn't support remove operation");
        }
    }

    internal struct BitArray32 : IList<bool>
    {
        private const int Length = 32;
        private uint _value;

        public BitArray32(uint value)
        {
            _value = value;
        }

        public bool this[int index]
        {
            get => (_value & (uint)(1 << index)) != 0;
            set
            {
                if (value)
                    _value |= (uint)(1 << index);
                else
                    _value &= (uint)(~(1 << index));
            }
        }

        public int Count => Length;
        public bool IsReadOnly => false;

        public void Insert(int index, bool item)
        {
            if (index > Length)
                throw new ArgumentOutOfRangeException($"Index '{index}' is outside of the bounds of {nameof(BitArray32)}");

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

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Clear() => _value = 0;

        public bool Contains(bool item) => IndexOf(item) != -1;

        public void CopyTo(bool[] array, int arrayIndex)
        {
            for (int i = 0; i < Length; i++)
            {
                array[arrayIndex + i] = this[i];
            }
        }

        public void Add(bool item)
        {
            throw new InvalidOperationException($"{nameof(BitArray32)} doesn't support add operation");
        }

        public void RemoveAt(int index)
        {
            throw new InvalidOperationException($"{nameof(BitArray32)} doesn't support remove operation");
        }

        public bool Remove(bool item)
        {
            throw new InvalidOperationException($"{nameof(BitArray32)} doesn't support remove operation");
        }
    }
}