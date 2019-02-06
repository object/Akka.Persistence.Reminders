#region copyright
// -----------------------------------------------------------------------
//  <copyright file="ReminderSpec.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.IO;
using System.Linq;
using System.Threading;
using Akka.Actor;
using Akka.Configuration;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Akka.Persistence.Reminders.Tests
{
    public class ReminderSpec : TestKit.Xunit.TestKit
    {
        public enum ScheduleType
        {
            Once,
            Repeat,
            Cron
        }

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

            protected override bool ShouldTrigger(Schedule schedule, DateTime now)
            {
                if (schedule.Message.ToString() == ImmediateMsg)
                    return true;

                return base.ShouldTrigger(schedule, now);
            }
        }

        public const string ImmediateMsg = "FIRE IMMEDIATELLY";
        private readonly IActorRef reminder;
        private readonly IActorRef journalRef;
        private readonly IActorRef snapshotStoreRef;

        private int seqNrCounter = 0;

        private static readonly Config TestConfig = ConfigurationFactory.ParseString(@"
                akka.persistence.snapshot-store.local.dir = ""target/snapshots-ReminderSpec/""")
            .WithFallback(Reminder.DefaultConfig);

        public ReminderSpec(ITestOutputHelper output) : base(TestConfig, output: output)
        {
            DeleteSnapshotFile();

            var settings = ReminderSettings.Default
                .WithTickInterval(TimeSpan.FromMilliseconds(500))
                .WithSnapshotInterval(5);

            var persistence = Persistence.Instance.Apply(Sys);
            journalRef = persistence.JournalFor(null);
            snapshotStoreRef = persistence.SnapshotStoreFor(null);

            reminder = Sys.ActorOf(Props.Create(() => new TestReminder(settings)), "reminder");
        }

        protected string Pid { get; } = ReminderSettings.Default.PersistenceId;

        protected override void AfterAll()
        {
            base.AfterAll();
            DeleteSnapshotFile();
        }

        [Fact]
        public void Reminder_must_schedule_tasks_as_events()
        {
            var at = DateTime.UtcNow.AddSeconds(2);
            var s1 = CreateSchedule(ImmediateMsg, at);

            reminder.Tell(s1, TestActor);
            ExpectMsg(ImmediateMsg + "-ack");
            ExpectMsg(ImmediateMsg);
        }

        [Fact]
        public void Reminder_must_return_its_state_when_requested()
        {
            var at = DateTime.UtcNow.AddSeconds(2);
            var s1 = CreateSchedule("A", at);
            var s2 = CreateSchedule("B", at);

            reminder.Tell(s1, TestActor);
            reminder.Tell(s2, TestActor);

            ExpectMsg("A-ack");
            ExpectMsg("B-ack");

            reminder.Tell(Reminder.GetState.Instance, TestActor);

            var expected = Reminder.State.Empty
                .AddEntry(CreateSchedule("A", at, withAck: false))
                .AddEntry(CreateSchedule("B", at, withAck: false));

            ExpectMsg(expected);
        }

        [Fact]
        public void Reminder_must_complete_task_after_sending_a_message()
        {
            var at = DateTime.UtcNow.AddSeconds(2);
            var s1 = CreateSchedule(ImmediateMsg, at);

            reminder.Tell(s1, TestActor);
            ExpectMsg(ImmediateMsg + "-ack");
            ExpectMsg(ImmediateMsg);

            Thread.Sleep(100); // wait a while, as task is triggered before Reminder.Completed is emitted

            journalRef.Tell(new ReplayMessages(0, long.MaxValue, 100, Pid, TestActor));
            ExpectEvent(Pid, 1, new Reminder.Scheduled(CreateSchedule(ImmediateMsg, at, withAck: false)));
            ExpectEvent(Pid, 2, (Reminder.Completed c) => c.TaskId == s1.TaskId);

            ExpectMsg<RecoverySuccess>();
        }

        [Fact]
        public void Reminder_must_occasionally_snapshot_its_state()
        {
            var at = DateTime.UtcNow.AddSeconds(2);
            var state = Reminder.State.Empty;
            for (int i = 0; i < 7; i++)
            {
                var msg = "A" + i;
                reminder.Tell(CreateSchedule(msg, at), TestActor);
                ExpectMsg(msg + "-ack");

                if (i < 5) state = state.AddEntry(CreateSchedule(msg, at, withAck: false));
            }

            // according to modified settings after 5 tries we should get a snapshot
            Thread.Sleep(500); // wait a little - snapshots are async

            snapshotStoreRef.Tell(new LoadSnapshot(Pid, SnapshotSelectionCriteria.Latest, long.MaxValue));
            ExpectMsg<LoadSnapshotResult>(r => Equals(r.Snapshot.Snapshot, state) && r.Snapshot.Metadata.SequenceNr == 5);
        }

        [Fact]
        public void Reminder_must_recover_its_state_from_snapshots_and_events()
        {
            var at = DateTime.UtcNow.AddSeconds(2);

            // write snapshot (A,B) and 2 events (Scheduled(C), Completed(B))
            // the result should be: A, C
            var expected = Reminder.State.Empty
                .AddEntry(CreateSchedule("A", at, withAck: false))
                .AddEntry(CreateSchedule("B", at, withAck: false));

            seqNrCounter += 2;
            WriteSnapshot(expected);
            WriteEvents(new Reminder.Scheduled(CreateSchedule("C", at, withAck: false)), new Reminder.Completed("B-task", DateTime.UtcNow));

            reminder.Tell(Crash.Instance);
            reminder.Tell(Reminder.GetState.Instance, TestActor);

            ExpectMsg(Reminder.State.Empty
                .AddEntry(CreateSchedule("A", at, withAck: false))
                .AddEntry(CreateSchedule("C", at, withAck: false)));
        }

        private Reminder.Schedule CreateSchedule(string msg, DateTime when, ScheduleType type = ScheduleType.Once, bool withAck = true)
        {
            switch (type)
            {
                case ScheduleType.Once:
                    return new Reminder.Schedule(
                        taskId: msg + "-task",
                        recipient: TestActor.Path,
                        message: msg,
                        triggerDateUtc: when,
                        ack: withAck ? msg + "-ack" : null);
                case ScheduleType.Repeat:
                    return new Reminder.ScheduleRepeatedly(
                        taskId: msg + "-task",
                        recipient: TestActor.Path,
                        message: msg,
                        triggerDateUtc: when,
                        repeatInterval: TimeSpan.FromSeconds(1),
                        ack: withAck ? msg + "-ack" : null);
                case ScheduleType.Cron:
                    return new Reminder.ScheduleCron(
                        taskId: msg + "-task",
                        recipient: TestActor.Path,
                        message: msg,
                        triggerDateUtc: when,
                        cronExpression: "0 12 * * *",
                        ack: withAck ? msg + "-ack" : null);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
        
        private void WriteSnapshot(object snapshot)
        {
            var meta = new SnapshotMetadata(Pid, seqNrCounter);
            snapshotStoreRef.Tell(new SaveSnapshot(meta, snapshot));
            ExpectMsg<SaveSnapshotSuccess>(s => s.Metadata.PersistenceId == Pid && s.Metadata.SequenceNr == seqNrCounter);
        }

        private void WriteEvents(params object[] events)
        {
            var persistents = events
                .Select(e => new Persistent(e, (++seqNrCounter), Pid, e.GetType().FullName))
                .ToArray();
            journalRef.Tell(new WriteMessages(persistents.Select(p => new AtomicWrite(p)), TestActor, 1));

            ExpectMsg<WriteMessagesSuccessful>();
            foreach (var p in persistents)
                ExpectMsg(new WriteMessageSuccess(p, 1));
        }

        private void ExpectEvent<T>(string persistenceId, int seqNr, T e) => ExpectEvent<T>(persistenceId, seqNr, evt => Equals(evt, e));

        private void ExpectEvent<T>(string persistenceId, int seqNr, Func<T, bool> predicate)
        {
            ExpectMsg<ReplayedMessage>(r =>
            {
                var p = r.Persistent;
                return p.PersistenceId == persistenceId && p.SequenceNr == seqNr && predicate((T)p.Payload);
            });
        }

        private void DeleteSnapshotFile()
        {
            var location = Sys.Settings.Config.GetString("akka.persistence.snapshot-store.local.dir");
            if (Directory.Exists(location))
            {
                Directory.Delete(location, true);
            }
        }
    }
}