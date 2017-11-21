#region copyright
// -----------------------------------------------------------------------
//  <copyright file="ReminderSpec.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using Akka.Actor;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Reminders.Tests
{
    public class TestReminder : Reminder
    {
        public TestReminder(ReminderSettings settings) : base(settings)
        {
        }

        protected override bool ShouldTrigger(Entry entry, DateTime now)
        {
            return base.ShouldTrigger(entry, now);
        }
    }

    public class ReminderSpec : TestKit.Xunit.TestKit
    {
        private readonly IActorRef reminder;

        public ReminderSpec(ITestOutputHelper output) : base(Reminder.DefaultConfig, output:output)
        {
            var settings = ReminderSettings.Default
                .WithSnapshotInterval(10);

            reminder = Sys.ActorOf(Props.Create(() => new TestReminder(settings)), "reminder");
        }

        [Fact]
        public void Reminder_must_return_its_state_when_requested()
        {
            
        }

        [Fact]
        public void Reminder_must_schedule_tasks_as_events()
        {
            
        }

        [Fact]
        public void Reminder_must_complete_task_after_sending_a_message()
        {
            
        }

        [Fact]
        public void Reminder_must_occasinally_snapshot_its_state()
        {
            
        }

        [Fact]
        public void Reminder_must_recover_its_state_from_snapshots_and_events()
        {
            
        }
    }
}