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
    }

    public class SerializationSpec : TestKit.Xunit.TestKit
    {
        private readonly Serializer serializer;

        public SerializationSpec(ITestOutputHelper output) : base(Reminder.DefaultConfig, output: output)
        {
            serializer = Sys.Serialization.FindSerializerForType(typeof(IReminderFormat));
            serializer.Should().BeOfType<ReminderSerializer>();
        }

        [Fact]
        public void ReminderSerializer_must_serialize_Reminder_State()
        {
            var expected = Reminder.State.Empty.AddEntry(new Reminder.Entry("task-1", TestActor.Path, new TestMessage("hello"), DateTime.UtcNow, TimeSpan.FromHours(1)));
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
        public void ReminderSerializer_must_serialize_Reminder_Scheduled()
        {
            var expected = new Reminder.Scheduled(new Reminder.Entry("task-1", TestActor.Path, new TestMessage("hello"), DateTime.UtcNow));
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
            var binary = serializer.ToBinary(value);
            return (T) serializer.FromBinary(binary, null);
        }
    }
}