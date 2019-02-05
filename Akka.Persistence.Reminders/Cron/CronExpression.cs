#region copyright
// -----------------------------------------------------------------------
//  <copyright file="CronExpression.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2019 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Akka.Persistence.Reminders.Cron
{
    /// <summary>
    /// Representation of cron expressions (https://en.wikipedia.org/wiki/Cron).
    /// </summary>
    /// <example>
    /// <code>
    /// *    *    *    *    *    *
    /// ┬    ┬    ┬    ┬    ┬    ┬
    /// │    │    │    │    │    |
    /// │    │    │    │    │    └ day of week(0 - 7) (0 or 7 is Sun)
    /// │    │    │    │    └───── month(1 - 12)
    /// │    │    │    └────────── day of month(1 - 31)
    /// │    │    └─────────────── hour(0 - 23)
    /// │    └──────────────────── minute(0 - 59)
    /// └───────────────────────── second (0 - 59, optional)
    /// </code>
    /// </example>
    [Serializable]
    public class CronExpression : IEquatable<CronExpression>
    {
        private static readonly CronFieldDescriptor Second = SecondDescriptor.Default;
        private static readonly CronFieldDescriptor Minute = MinuteDescriptor.Default;
        private static readonly CronFieldDescriptor Hour = HourDescriptor.Default;
        private static readonly CronFieldDescriptor DayOfMonth = DayOfMonthDescriptor.Default;
        private static readonly CronFieldDescriptor Month = MonthDescriptor.Default;
        private static readonly CronFieldDescriptor DayOfWeek = DayOfWeekDescriptor.Default;

        private static byte[] EmptyArray = new byte[0];
        private static readonly char[] splitChars = { ' ' };
        private static readonly char[] segmentSplitChars = { ',' };

        private readonly string _expression;

        // Cron expression masks
        private readonly ArraySegment<byte> _seconds;
        private readonly ArraySegment<byte> _minutes;
        private readonly ArraySegment<byte> _hours;
        private readonly ArraySegment<byte> _months;
        private readonly BitArray32 _daysOfMonth;
        private readonly BitArray32 _workDaysOfMonth;
        private readonly BitArray32 _daysOfWeekSpecific;
        private readonly BitArray8 _daysOfWeek;
        private readonly BitArray8 _lastDaysOfWeek;

        #region parsing

        /// <summary>
        /// Parses given cron expression.
        /// </summary>
        /// <param name="cronExpr"></param>
        /// <returns></returns>
        public static CronExpression Parse(string cronExpr)
        {
            var definition = ReplaceDefinitions(cronExpr.Trim());
            return new CronExpression(definition);
        }

        public CronExpression(string expression)
        {
            if (expression is null)
            {
                throw new ArgumentNullException(nameof(expression), "Cron expression must be provided");
            }

            var fields = expression.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);

            if (fields.Length > 6 || fields.Length < 5)
            {
                throw new CronParsingException($"Failed to parse cron expression '{expression}': valid expression must have 5 or 6 fields.");
            }

            _seconds = Second.Values;
            _minutes = Minute.Values;
            _hours = Hour.Values;
            _months = Month.Values;
            _daysOfMonth = new BitArray32();
            _workDaysOfMonth = new BitArray32();
            _daysOfWeekSpecific = new BitArray32();
            _daysOfWeek = new BitArray8();
            _lastDaysOfWeek = new BitArray8();

            if (fields.Length == 5)
                _seconds = new ArraySegment<byte>(new byte[] { 0 }, 0, 1);
            else
                if (!ParseField(fields[0], Second, out _seconds)) throw new CronParsingException($"Couldn't parse 'seconds' component of cron expression: '{fields[0]}'");

            if (!ParseField(fields[1], Minute, out _minutes)) throw new CronParsingException($"Couldn't parse 'minutes' component of cron expression: '{fields[1]}'");
            if (!ParseField(fields[2], Hour, out _hours)) throw new CronParsingException($"Couldn't parse 'hours' component of cron expression: '{fields[2]}'");
            if (!ParseDayOfMonth(fields[3], DayOfMonth, ref _daysOfMonth, ref _workDaysOfMonth))
                throw new CronParsingException($"Couldn't parse 'day-of-month' component of cron expression: '{fields[3]}'");
            if (!ParseField(fields[4], Month, out _months)) throw new CronParsingException($"Couldn't parse 'months' component of cron expression: '{fields[4]}'");
            if (!ParseDayOfWeek(fields[5], DayOfWeek, ref _daysOfWeek, ref _lastDaysOfWeek, ref _daysOfWeekSpecific))
                throw new CronParsingException($"Couldn't parse 'day-of-week' component of cron expression: '{fields[5]}'");
        }

        private BitArray8 ArraySegmentToBitArray8(ref ArraySegment<byte> data)
        {
            var result = new BitArray8();
            foreach (var b in data) result[b] = true;
            return result;
        }

        private BitArray32 ArraySegmentToBitArray32(ref ArraySegment<byte> data)
        {
            var result = new BitArray32();
            foreach (var b in data) result[b] = true;
            return result;
        }

        private bool ParseDayOfWeek(string fieldValue, CronFieldDescriptor descriptor, ref BitArray8 daysOfWeek, ref BitArray8 lastDaysOfWeek, ref BitArray32 daysOfWeekSpecific)
        {
            if (ParseField(fieldValue, descriptor, out var dow))
            {
                daysOfWeek = ArraySegmentToBitArray8(ref dow);
                return true;
            }

            var suffix = fieldValue[fieldValue.Length - 1];
            if (suffix == 'L')
            {
                fieldValue = fieldValue.Substring(0, fieldValue.Length - 1);
                if (ParseField(fieldValue, descriptor, out dow))
                {
                    lastDaysOfWeek = ArraySegmentToBitArray8(ref dow);
                    lastDaysOfWeek[7] = true; // use last bit to mark that lastDaysOfWeek is used
                    return true;
                }
            }

            var idx = fieldValue.IndexOf('#');
            if (idx != -1)
            {
                var left = fieldValue.Substring(0, idx);
                var right = fieldValue.Substring(idx + 1);
                if (byte.TryParse(left, out var day) && byte.TryParse(right, out var step))
                {
                    daysOfWeekSpecific[day % 7 + step * 7] = true;
                    daysOfWeekSpecific[31] = true; // use last bit to mark that daysOfWeekSpecific is used
                }
            }

            return false;
        }

        private bool ParseDayOfMonth(string fieldValue, CronFieldDescriptor descriptor, ref BitArray32 daysOfMonth, ref BitArray32 workDaysOfMonth)
        {
            if (fieldValue.EndsWith("L"))
            {
                if (ParseField(fieldValue.Substring(0, fieldValue.Length - 1), descriptor, out var dom))
                {
                    daysOfMonth = ArraySegmentToBitArray32(ref dom);
                    daysOfMonth[31] = true; // mark for last day of month used
                    return true;
                }
            }
            else if (fieldValue.EndsWith("LW"))
            {
                if (ParseField(fieldValue.Substring(0, fieldValue.Length - 2), descriptor, out var dom))
                {
                    daysOfMonth = ArraySegmentToBitArray32(ref dom);
                    workDaysOfMonth[31] = true; // mark for last work day of month used
                    return true;
                }
            }
            else if (fieldValue.EndsWith("W"))
            {
                if (byte.TryParse(fieldValue.Substring(0, fieldValue.Length - 1), out var day))
                {
                    workDaysOfMonth[day] = true;
                    workDaysOfMonth[31] = true; // mark for last day of month used
                    return true;
                }
            }
            else if (ParseField(fieldValue, descriptor, out var dom))
            {
                daysOfMonth = ArraySegmentToBitArray32(ref dom);
                return true;
            }

            return false;
        }

        private static bool ParseField(string fieldValue, CronFieldDescriptor descriptor, out ArraySegment<byte> result)
        {
            if (fieldValue == "*")
            {
                result = descriptor.Values;
                return true;
            }

            byte min = descriptor.MinValue, max = descriptor.MaxValue, step = 0;

            // X
            if (byte.TryParse(fieldValue, out min))
            {
                result = new ArraySegment<byte>(CronFieldDescriptor.AllValues, min, max);
                return true;
            }

            // min-max
            var rangeIdx = fieldValue.IndexOf('-');
            if (rangeIdx != -1)
            {
                min = descriptor.Parse(fieldValue.Substring(0, rangeIdx));

                // min-max/step
                var stepIdx = fieldValue.IndexOf('/');
                if (stepIdx != -1)
                {
                    var maxStr = fieldValue.Substring(rangeIdx + 1, stepIdx - rangeIdx - 1);
                    var stepStr = fieldValue.Substring(stepIdx + 1);

                    max = descriptor.Parse(maxStr);
                    step = descriptor.Parse(stepStr);
                }

                if (min >= max)
                {
                    throw new CronParsingException($"Specified field '{descriptor.Name}' value '{fieldValue}' min value is greater or equal max value in range.");
                }

                if (step >= max - min)
                {
                    throw new CronParsingException($"Specified field '{descriptor.Name}' value '{fieldValue}' step value is beyond boundaries of min-max range.");
                }

                result = BuildSteps(min, max, step);
                return true;
            }

            // X/Y
            var stepIndex = fieldValue.IndexOf('/');
            if (stepIndex != -1)
            {
                min = descriptor.Parse(fieldValue.Substring(0, stepIndex));
                step = descriptor.Parse(fieldValue.Substring(stepIndex + 1));
                result = BuildSteps(min, max, step);
                return true;
            }

            // X,Y,Z
            var enumerations = fieldValue.Split(segmentSplitChars);
            if (enumerations.Length > 1)
            {
                var values = new byte[enumerations.Length];
                for (int i = 0; i < enumerations.Length; i++)
                {
                    values[i] = descriptor.Parse(enumerations[i]);
                }
                result = new ArraySegment<byte>(values, 0, enumerations.Length);
                return true;
            }
            
            result = new ArraySegment<byte>(EmptyArray, 0, 0);
            return false;
        }

        private static ArraySegment<byte> BuildSteps(byte min, byte max, byte step)
        {
            if (step <= 1)
            {
                return new ArraySegment<byte>(CronFieldDescriptor.AllValues, min, max-min);
            }
            else
            {
                var list = new List<byte>();
                for (int i = min; i < max; i += step)
                {
                    list.Add(CronFieldDescriptor.AllValues[i]);
                }

                var values = list.ToArray();
                return new ArraySegment<byte>(values, 0, values.Length);
            }
        }

        private static string ReplaceDefinitions(string definition)
        {
            switch (definition)
            {
                case "@yearly":
                case "@annually": return "0 0 0 1 1 *";
                case "@monthly": return "0 0 0 1 * *";
                case "@weekly": return "0 0 0 * * 0";
                case "@daily": return "0 0 0 * * *";
                case "@hourly": return "0 0 * * * *";
                default: return definition;
            }
        }

        /// <summary>
        /// Returns an enumerable, which will compute following execution times accordingly
        /// to current <see cref="CronExpression"/>, starting from a given date.
        /// </summary>
        /// <param name="startFrom"></param>
        /// <returns></returns>
        public IEnumerable<DateTime> GetExecutionSequence(DateTime startFrom)
        {
            var date = startFrom;
            while (true)
            {
                date = NextExecutionDate(date);
                yield return date;
            }
        }

        #endregion

        #region date calculation

        /// <summary>
        /// Computes the next execution time starting from a given <paramref name="startFrom"/> date.
        /// </summary>
        /// <param name="startFrom"></param>
        /// <returns></returns>
        public DateTime NextExecutionDate(DateTime startFrom)
        {
            throw new NotImplementedException();
        }

        private DateTime NextSecond(DateTime previous)
        {
            throw new NotImplementedException();
        }

        private DateTime NextMinute(DateTime previous)
        {
            throw new NotImplementedException();
        }

        private DateTime NextHour(DateTime previous)
        {
            throw new NotImplementedException();
        }

        private DateTime NextDayOfMonth(DateTime previous)
        {
            throw new NotImplementedException();
        }

        private DateTime NextMonth(DateTime previous)
        {
            throw new NotImplementedException();
        }

        private DateTime NextDayOfWeek(DateTime previous)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region overrides

        public override string ToString() => _expression;

        public bool Equals(CronExpression other)
        {
            if (other is null) return false;
            return this._expression == other._expression;
        }

        public override bool Equals(object obj) => obj is CronExpression expression && Equals(expression);

        public override int GetHashCode() => _expression.GetHashCode();

        #endregion

        #region serialization

        //protected CronExpression(SerializationInfo info, StreamingContext context) : this(info.GetString("cron"))
        //{
        //}

        //public void GetObjectData(SerializationInfo info, StreamingContext context)
        //{
        //    info.AddValue("cron", _expression);
        //}

        #endregion
    }
}
