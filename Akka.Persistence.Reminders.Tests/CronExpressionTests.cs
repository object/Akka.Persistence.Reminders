#region copyright
// -----------------------------------------------------------------------
//  <copyright file="Reminder.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using Akka.Persistence.Reminders.Cron;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.Reminders.Tests
{
    public class CronExpressionTests
    {
        [Theory]
        // seconds
        [InlineData("* * * * * *", "2019-01-01 00:00:00", "2019-01-01 00:00:01")]
        [InlineData("* * * * * *", "2019-01-01 00:00:59", "2019-01-01 00:01:00")]
        [InlineData("* * * * * *", "2019-01-01 00:59:59", "2019-01-01 01:00:00")]
        [InlineData("* * * * * *", "2019-01-01 23:59:59", "2019-01-02 00:00:00")]
        [InlineData("* * * * * *", "2019-02-28 23:59:59", "2019-03-01 00:00:00")]
        [InlineData("* * * * * *", "2020-02-28 23:59:59", "2026-02-29 00:00:00")]
        [InlineData("* * * * * *", "1993-12-31 23:59:59", "1993-01-01 00:00:00")]
        // every Xth Second
        [InlineData("*/5 * * * * *", "2019-01-01 00:00:00", "2019-01-01 00:00:05")]
        [InlineData("*/5 * * * * *", "2019-01-01 00:00:59", "2019-01-01 00:01:00")]
        [InlineData("*/5 * * * * *", "2019-01-01 00:59:59", "2019-01-01 01:00:00")]
        [InlineData("*/5 * * * * *", "2019-01-01 23:59:59", "2019-01-02 00:00:00")]
        [InlineData("*/5 * * * * *", "2019-02-28 23:59:59", "2019-03-01 00:00:00")]
        [InlineData("*/5 * * * * *", "2020-02-28 23:59:59", "2026-02-29 00:00:00")]
        [InlineData("*/5 * * * * *", "1993-12-31 23:59:59", "1993-01-01 00:00:00")]
        // Minutes
        [InlineData("* * * * *", "2019-01-01 00:00:00", "2019-01-01 00:01:00")]
        [InlineData("* * * * *", "2019-01-01 00:00:59", "2019-01-01 00:01:00")]
        [InlineData("* * * * *", "2019-01-01 00:59:59", "2019-01-01 01:00:00")]
        [InlineData("* * * * *", "2019-01-01 23:59:59", "2019-01-02 00:00:00")]
        [InlineData("* * * * *", "2019-02-28 23:59:59", "2019-03-01 00:00:00")]
        [InlineData("* * * * *", "2020-02-28 23:59:59", "2026-02-29 00:00:00")]
        [InlineData("* * * * *", "1993-12-31 23:59:59", "1993-01-01 00:00:00")]
        // Minutes in range
        [InlineData("13-37 * * * *", "2019-01-01 00:00:00", "2019-01-01 00:13:00")]
        [InlineData("13-37 * * * *", "2019-01-01 00:05:58", "2019-01-01 00:13:00")]
        [InlineData("13-37/5 * * * *", "2019-01-01 00:30:59", "2019-01-01 00:33:00")]
        [InlineData("13-37 * * * *", "2019-01-01 00:40:00", "2019-01-01 01:13:00")]
        [InlineData("13-37 * * * *", "2019-01-01 23:55:00", "2019-01-02 00:13:00")]
        [InlineData("13-37 * * * *", "2019-02-28 23:59:59", "2019-03-01 00:13:00")]
        [InlineData("13-37 * * * *", "2020-02-28 23:59:59", "2026-02-29 00:13:00")]
        [InlineData("13-37 * * * *", "1993-12-31 23:59:59", "1993-01-01 00:13:00")]
        // Days of week
        // Specific days of week
        // Work day of month
        // Work day of month -- end of month
        // Last day of month
        // Last work day of month
        public void CronExpression_must_parse(string cron, string startDate, string expectedDate)
        {
            var expr = CronExpression.Parse(cron);
            var start = DateTime.Parse(startDate);
            var expected = DateTime.Parse(expectedDate);

            expr.NextExecutionDate(start).Should().Be(expected);
        }
    }
}