#region copyright
// -----------------------------------------------------------------------
//  <copyright file="CronFieldDescriptor.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2019 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Akka.Persistence.Reminders.Cron
{
    internal class CronFieldDescriptor
    {
        public static readonly byte[] AllValues =
        {
            0, 1, 2, 3, 4, 5, 6, // up to days of week
            7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, // up to days of month
            32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59 // up to seconds, minutes, hours
        };

        public readonly string Name;
        public readonly byte MinValue;
        public readonly byte MaxValue;
        public readonly ArraySegment<byte> Values;

        protected CronFieldDescriptor(string name, byte minValue, byte maxValue, Regex valueRegex)
        {
            Name = name;
            MinValue = minValue;
            MaxValue = maxValue;
            Values = new ArraySegment<byte>(AllValues, minValue, MaxValue-MinValue);
        }

        public virtual byte Parse(string value)
        {
            var parsed = byte.Parse(value);
            if (parsed > MaxValue || parsed < MinValue)
            {
                throw new CronParsingException($"Value of field cron expression field '{Name}' of '{value}' doesn't fit in specified range [{MinValue}, {MaxValue}]");
            }

            return parsed;
        }
    }

    internal sealed class SecondDescriptor : CronFieldDescriptor
    {
        public static readonly SecondDescriptor Default = new SecondDescriptor();

        private SecondDescriptor() 
            : base("second", 0, 59, new Regex("[0-9]|[1-5][0-9]", RegexOptions.Compiled))
        {
        }
    }

    internal sealed class MinuteDescriptor : CronFieldDescriptor
    {
        public static readonly MinuteDescriptor Default = new MinuteDescriptor();
        private MinuteDescriptor()
            : base("minute", 0, 59, new Regex("[0-9]|[1-5][0-9]", RegexOptions.Compiled))
        {
        }
        
    }

    internal sealed class HourDescriptor : CronFieldDescriptor
    {
        public static readonly HourDescriptor Default = new HourDescriptor();
        private HourDescriptor()
            : base("hour", 0, 23, new Regex("[0-9]|1[0-9]|2[0-3]", RegexOptions.Compiled))
        {
        }
    }

    internal sealed class DayOfMonthDescriptor : CronFieldDescriptor
    {
        public static readonly DayOfMonthDescriptor Default = new DayOfMonthDescriptor();
        private DayOfMonthDescriptor()
            : base("day-of-month", 0, 31, new Regex("0?[1-9]|[12][0-9]|3[01]", RegexOptions.Compiled))
        {
        }
    }

    internal sealed class MonthDescriptor : CronFieldDescriptor
    {
        private static readonly ImmutableDictionary<string, byte> Tokens = ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase,
            new Dictionary<string, byte>
            {
                ["1"] = 1,
                ["2"] = 2,
                ["3"] = 3,
                ["4"] = 4,
                ["5"] = 5,
                ["6"] = 6,
                ["7"] = 7,
                ["8"] = 8,
                ["9"] = 9,
                ["10"] = 10,
                ["11"] = 11,
                ["12"] = 12,

                ["jan"] = 1,
                ["feb"] = 2,
                ["mar"] = 3,
                ["apr"] = 4,
                ["may"] = 5,
                ["jun"] = 6,
                ["jul"] = 7,
                ["aug"] = 8,
                ["sep"] = 9,
                ["oct"] = 10,
                ["nov"] = 11,
                ["dec"] = 12,

                ["january"] = 1,
                ["february"] = 2,
                ["march"] = 3,
                ["april"] = 4,
                ["may"] = 5,
                ["june"] = 6,
                ["july"] = 7,
                ["august"] = 8,
                ["september"] = 9,
                ["october"] = 10,
                ["november"] = 11,
                ["december"] = 12,
            });

        public static readonly MonthDescriptor Default = new MonthDescriptor();
        private MonthDescriptor()
            : base("month", 1, 12, new Regex("0?[1-9]|1[012]|jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec|january|february|march|april|march|april|june|july|august|september|october|november|december", RegexOptions.Compiled|RegexOptions.IgnoreCase))
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override byte Parse(string value)
        {
            if (Tokens.TryGetValue(value, out var num)) return num;

            throw new CronParsingException($"Tried to parse cron expression, but value '{value}' is not valid month. Valid values are: {string.Join(", ", Tokens.Keys)}");
        }
    }

    internal sealed class DayOfWeekDescriptor : CronFieldDescriptor
    {
        private static readonly ImmutableDictionary<string, byte> Tokens = ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase,
            new Dictionary<string, byte>
            {
                ["0"] = 0,
                ["1"] = 1,
                ["2"] = 2,
                ["3"] = 3,
                ["4"] = 4,
                ["5"] = 5,
                ["6"] = 6,
                ["7"] = 0,

                ["sun"] = 0,
                ["mon"] = 1,
                ["tue"] = 2,
                ["wed"] = 3,
                ["thu"] = 4,
                ["fri"] = 5,
                ["sat"] = 6,

                ["sunday"] = 0,
                ["monday"] = 1,
                ["tuesday"] = 2,
                ["wednesday"] = 3,
                ["thursday"] = 4,
                ["friday"] = 5,
                ["saturday"] = 6,
            });

        public static readonly DayOfWeekDescriptor Default = new DayOfWeekDescriptor();
        private DayOfWeekDescriptor()
            : base("day-of-week", 0, 6, new Regex("0?[0-7]|sun|mon|tue|wed|thu|fri|sat|sunday|monday|tuesday|wednesday|thursday|friday|saturday", RegexOptions.Compiled|RegexOptions.IgnoreCase))
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public override byte Parse(string value)
        {
            if (Tokens.TryGetValue(value, out var num)) return num;

            throw new CronParsingException($"Tried to parse cron expression, but value '{value}' is not valid day-of-week. Valid values are: {string.Join(", ", Tokens.Keys)}");
        }
    }
}