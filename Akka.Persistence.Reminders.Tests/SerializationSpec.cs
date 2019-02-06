#region copyright
// -----------------------------------------------------------------------
//  <copyright file="SerializationSpec.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Immutable;
using Akka.Persistence.Reminders.Serialization;
using Akka.Serialization;
using Cronos;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Reminders.Tests
{
    public sealed class TestMessage: IEquatable<TestMessage>
    {
        public TestMessage(string content)
        {
            Content = content;
        }

        public string Content { get; }

        public bool Equals(TestMessage other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Content, other.Content);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is TestMessage && Equals((TestMessage) obj);
        }

        public override int GetHashCode()
        {
            return (Content != null ? Content.GetHashCode() : 0);
        }

        public override string ToString() => Content;
    }

    public class SerializationSpec : TestKit.Xunit.TestKit
    {
        public SerializationSpec(ITestOutputHelper output) : base(Reminder.DefaultConfig, output: output)
        {
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_State()
        {
            var expected = Reminder.State.Empty
                .AddEntry(new Reminder.Schedule("task-1", TestActor.Path, new TestMessage("hello1"), DateTime.UtcNow))
                .AddEntry(new Reminder.ScheduleRepeatedly("task-2", TestActor.Path, new TestMessage("hello2"), DateTime.UtcNow, TimeSpan.FromHours(1)))
                .AddEntry(new Reminder.ScheduleCron("task-3", TestActor.Path, new TestMessage("hello3"), DateTime.UtcNow, "0 0 * * MON-FRI"));
            var actual = Roundtrip(expected);

            actual.Should().Be(expected);
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_Completed()
        {
            var expected = new Reminder.Completed("task-1", DateTime.UtcNow);
            var actual = Roundtrip(expected);

            actual.Should().Be(expected);
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_Schedule()
        {
            var expected = new Reminder.Schedule("task-1", TestActor.Path, new TestMessage("hello"), DateTime.UtcNow, ack: "reply");
            var actual = Roundtrip(expected);

            actual.Should().Be(expected);
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_ScheduleRepeatedly()
        {
            var expected = new Reminder.ScheduleRepeatedly("task-1", TestActor.Path, new TestMessage("hello"), DateTime.UtcNow, TimeSpan.FromMinutes(5), ack: "reply");
            var actual = Roundtrip(expected);

            actual.Should().Be(expected);
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_ScheduleCron()
        {
            var expected = new Reminder.ScheduleCron("task-1", TestActor.Path, new TestMessage("hello"), DateTime.UtcNow, "*/5 * * * *", ack: "reply");
            var actual = Roundtrip(expected);

            actual.Should().Be(expected);
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_Scheduled()
        {
            var expected = new Reminder.Scheduled(new Reminder.Schedule("task-1", TestActor.Path, new TestMessage("hello"), DateTime.UtcNow));
            var actual = Roundtrip(expected);

            actual.Should().Be(expected);
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_GetState()
        {
            var expected = Reminder.GetState.Instance;
            var actual = Roundtrip(expected);

            actual.Should().Be(expected);
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_Cancel()
        {
            var expected = new Reminder.Cancel("task-id", new TestMessage("hello"));
            var actual = Roundtrip(expected);

            actual.Should().Be(expected);
        }

        private T Roundtrip<T>(T value)
        {
            var serializer = (ReminderSerializer)Sys.Serialization.FindSerializerFor(value);
            var binary = serializer.ToBinary(value);
            return (T) serializer.FromBinary(binary, serializer.Manifest(value));
        }
    }
}