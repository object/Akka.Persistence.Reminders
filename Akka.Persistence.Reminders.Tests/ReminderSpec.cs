#region copyright
// -----------------------------------------------------------------------
//  <copyright file="ReminderSpec.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Reminders.Tests
{
    public class ReminderSpec : TestKit.Xunit.TestKit
    {
        public sealed class Crash
        {
            public static readonly Crash Instance = new Crash();
            private Crash() { }
        }

        public class TestReminder : Reminder
        {
            public TestReminder(ReminderSettings settings) : base(settings)
            {
                Command<Crash>(_ => throw new Exception("BOOM!"));
            }

            protected override bool ShouldTrigger(Entry entry, DateTime now)
            {
                if (entry.Message.ToString() == ImmediateMsg)
                    return true;

                return base.ShouldTrigger(entry, now);
            }
        }

        public const string ImmediateMsg = "FIRE IMMEDIATELLY";
        private readonly IActorRef reminder;
        private readonly IActorRef journalRef;
        private readonly IActorRef snapshotStoreRef;

        private int seqNrCounter = 1;

        public ReminderSpec(ITestOutputHelper output) : base(Reminder.DefaultConfig, output:output)
        {
            var settings = ReminderSettings.Default
                .WithTickInterval(TimeSpan.FromMilliseconds(500))
                .WithSnapshotInterval(5);

            var persistence = Persistence.Instance.Apply(Sys);
            journalRef = persistence.JournalFor(null);
            snapshotStoreRef = persistence.SnapshotStoreFor(null);

            reminder = Sys.ActorOf(Props.Create(() => new TestReminder(settings)), "reminder");
        }

        protected string Pid { get; } = ReminderSettings.Default.PersistenceId;

        [Fact]
        public void Reminder_must_schedule_tasks_as_events()
        {
            var s1 = CreateSchedule(ImmediateMsg);

            reminder.Tell(s1, TestActor);
            ExpectMsg(ImmediateMsg + "-ack");
            ExpectMsg(ImmediateMsg);
        }

        [Fact]
        public void Reminder_must_return_its_state_when_requested()
        {
            var s1 = CreateSchedule("A");
            var s2 = CreateSchedule("B");

            reminder.Tell(s1, TestActor);
            reminder.Tell(s2, TestActor);

            ExpectMsg("A-ack");
            ExpectMsg("B-ack");

            reminder.Tell(Reminder.GetState.Instance, TestActor);

            var expected = Reminder.State.Empty
                .AddEntry(CreateEntry("A"))
                .AddEntry(CreateEntry("B"));

            ExpectMsg(expected);
        }

        [Fact]
        public void Reminder_must_complete_task_after_sending_a_message()
        {
            var s1 = CreateSchedule(ImmediateMsg);

            reminder.Tell(s1, TestActor);
            ExpectMsg(ImmediateMsg + "-ack");
            ExpectMsg(ImmediateMsg);

            journalRef.Tell(new ReplayMessages(0, long.MaxValue, 100, Pid, TestActor));
            ExpectMsg<RecoverySuccess>();
        }

        [Fact]
        public void Reminder_must_occasinally_snapshot_its_state()
        {
            var state = Reminder.State.Empty;
            for (int i = 0; i < 7; i++)
            {
                var msg = "A" + i;
                reminder.Tell(CreateSchedule(msg), TestActor);
                ExpectMsg(msg + "-ack");

                if (i < 5) state = state.AddEntry(CreateEntry(msg));
            }

            // according to modified settings after 5 tries we should get a snapshot
            Thread.Sleep(500); // wait a little - snapshots are async
            
            snapshotStoreRef.Tell(new LoadSnapshot(Pid, SnapshotSelectionCriteria.Latest, long.MaxValue));
            ExpectMsg(new LoadSnapshotResult(new SelectedSnapshot(new SnapshotMetadata(Pid, 5), state), 5));
        }

        [Fact]
        public void Reminder_must_recover_its_state_from_snapshots_and_events()
        {
            // write snapshot (A,B) and 2 events (Scheduled(C), Completed(B))
            // the result should be: A, C
            var expected = Reminder.State.Empty
                .AddEntry(CreateEntry("A"))
                .AddEntry(CreateEntry("B"));

            seqNrCounter += 2;
            WriteSnapshot(expected);
            WriteEvents(new Reminder.Scheduled(CreateEntry("C")), new Reminder.Completed("B-task", DateTime.UtcNow));

            reminder.Tell(Crash.Instance);
            reminder.Tell(Reminder.GetState.Instance, TestActor);

            ExpectMsg(Reminder.State.Empty
                .AddEntry(CreateEntry("A"))
                .AddEntry(CreateEntry("C")));
        }

        private Reminder.Schedule CreateSchedule(string msg, bool repeat = false)
        {
            return new Reminder.Schedule(
                taskId: msg + "-task",
                receiver: TestActor.Path,
                message: msg,
                triggerDateUtc: DateTime.UtcNow.AddSeconds(2),
                repeatInterval: repeat ? (TimeSpan?)TimeSpan.FromSeconds(1) : null,
                ack: msg + "-ack");
        }

        private Reminder.Entry CreateEntry(string msg, bool repeat = false)
        {
            return new Reminder.Entry(
                taskId: msg + "-task",
                receiver: TestActor.Path,
                message: msg,
                triggerDateUtc: DateTime.UtcNow.AddSeconds(2),
                repeatInterval: repeat ? (TimeSpan?)TimeSpan.FromSeconds(1) : null);
        }

        private void WriteSnapshot(object snapshot)
        {
            var meta = new SnapshotMetadata(Pid, seqNrCounter);
            snapshotStoreRef.Tell(new SaveSnapshot(meta, snapshot));
            ExpectMsg(new SaveSnapshotSuccess(meta));
        }

        private void WriteEvents(params object[] events)
        {
            var persistents = events
                .Select(e => new Persistent(e, seqNrCounter++, Pid, e.GetType().FullName))
                .ToArray();
            journalRef.Tell(new WriteMessages(persistents.Select(p => new AtomicWrite(p)), TestActor, 1));

            ExpectMsg<WriteMessagesSuccessful>();
            foreach (var p in persistents)
                ExpectMsg(new WriteMessageSuccess(p, 1));
        }
    }
}