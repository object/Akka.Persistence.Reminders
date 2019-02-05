#region copyright
// -----------------------------------------------------------------------
//  <copyright file="Utils.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2019 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;

namespace Akka.Persistence.Reminders.Cron
{
    internal static class Utils
    {
        public static int IndexOfOrGreater(in this BitArray64 data, int value, int max = BitArray64.Length)
        {
            while (value < max & !data[value]) value++;
            return value;
        }

        public static int IndexOfOrGreater(in this BitArray32 data, int value, int max = BitArray32.Length)
        {
            while (value < max & !data[value]) value++;
            return value;
        }

        public static int IndexOfOrGreater(in this BitArray16 data, int value, int max = BitArray16.Length)
        {
            while (value < max & !data[value]) value++;
            return value;
        }

        public static int IndexOfOrGreater(in this BitArray8 data, int value, int max = BitArray8.Length)
        {
            while (value < max & !data[value]) value++;
            return value;
        }

        /// <summary>
        /// An array with bitmaps representing numbers of days.
        /// </summary>
        public static readonly uint[] Masks31 =
        {
            0b_00000000_00000000_00000000_00000000,
            0b_00000000_00000000_00000000_00000001,
            0b_00000000_00000000_00000000_00000011,
            0b_00000000_00000000_00000000_00000111,
            0b_00000000_00000000_00000000_00001111,
            0b_00000000_00000000_00000000_00011111,
            0b_00000000_00000000_00000000_00111111,
            0b_00000000_00000000_00000000_01111111,
            0b_00000000_00000000_00000000_11111111,
            0b_00000000_00000000_00000001_11111111,
            0b_00000000_00000000_00000011_11111111,
            0b_00000000_00000000_00000111_11111111,
            0b_00000000_00000000_00001111_11111111,
            0b_00000000_00000000_00011111_11111111,
            0b_00000000_00000000_00111111_11111111,
            0b_00000000_00000000_01111111_11111111,
            0b_00000000_00000000_11111111_11111111,
            0b_00000000_00000001_11111111_11111111,
            0b_00000000_00000011_11111111_11111111,
            0b_00000000_00000111_11111111_11111111,
            0b_00000000_00001111_11111111_11111111,
            0b_00000000_00011111_11111111_11111111,
            0b_00000000_00111111_11111111_11111111,
            0b_00000000_01111111_11111111_11111111,
            0b_00000000_11111111_11111111_11111111,
            0b_00000001_11111111_11111111_11111111,
            0b_00000011_11111111_11111111_11111111,
            0b_00000111_11111111_11111111_11111111,
            0b_00001111_11111111_11111111_11111111,
            0b_00011111_11111111_11111111_11111111,
            0b_00111111_11111111_11111111_11111111,
            0b_01111111_11111111_11111111_11111111,
        };

        /// <summary>
        /// Byte mask for days from Sunday-Saturday.
        /// </summary>
        public const byte WorkDaysInWeek = 0b_00111110;

        /// <summary>
        /// Byte mask for work days in month, assuming standardized 31-days month.
        /// </summary>
        public static readonly uint[] WorkDaysInMonth =
        {
            0b_0110_0111110_0111110_0111110_0111110U, // Sunday
            0b_0111_0011111_0011111_0011111_0011111U, // Monday
            0b_0111_1001111_1001111_1001111_1001111U, // Tuesday
            0b_0111_1100111_1100111_1100111_1100111U, // Wednesday
            0b_0011_1110011_1110011_1110011_1110011U, // Thursday
            0b_0001_1111001_1111001_1111001_1111001U, // Friday
            0b_0100_1111100_1111100_1111100_1111100U, // Saturday
        };

        /// <summary>
        /// Mask used to represent number of days in a week.
        /// </summary>
        public static readonly byte[] Masks7 =
        {
            0b_00000000,
            0b_00000001,
            0b_00000011,
            0b_00000111,
            0b_00001111,
            0b_00011111,
            0b_00111111,
            0b_01111111,
        };

        /// <summary>
        /// Mask used to represent number of months.
        /// </summary>
        public const ushort Mask12 = 0b_00001111_11111111;

        /// <summary>
        /// Mask used to represent number of seconds, minutes, hours.
        /// </summary>
        public const ulong Mask60 = 0b_00001111_11111111_11111111_11111111_11111111_11111111_11111111_11111111;
    }
}