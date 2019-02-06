#region copyright
// -----------------------------------------------------------------------
//  <copyright file="Reminder.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;

namespace Akka.Persistence.Reminders
{
    /// <summary>
    /// A <see cref="Reminder"/> is an actor responsible for managing scheduled messages
    /// over long periods of time (minutes, hours, days or weeks). Unlike standard Akka.NET
    /// scheduler, it should NOT be used for tasks designed for sub-second frequencies.
    /// Advantage here is that scheduled tasks are persisted using Akka.Persistence mechanism, 
    /// making them durable in face of actor system or machine failures.
    /// </summary>
    public partial class Reminder : ReceivePersistentActor
    {
        /// <summary>
        /// A default set of configuration parameters used by the <see cref="Reminder"/> actor.
        /// </summary>
        public static Config DefaultConfig => ConfigurationFactory.FromResource<Reminder>("Akka.Persistence.Reminders.reference.conf");

        /// <summary>
        /// An actor <see cref="Akka.Actor.Props"/> for <see cref="Reminder"/> class setup using default settings.
        /// </summary>
        public static Props Props() => Actor.Props.Create(() => new Reminder());

        /// <summary>
        /// An actor <see cref="Akka.Actor.Props"/> for <see cref="Reminder"/> class setup using provided <paramref name="settings"/>.
        /// </summary>
        public static Props Props(ReminderSettings settings) => Actor.Props.Create(() => new Reminder(settings));

        public override string PersistenceId { get; }

        private readonly ICancelable tickTask;
        private State state = State.Empty;
        private long counter = 0L;
        private readonly ReminderSettings settings;

        private readonly Action<IReminderEvent> UpdateState;

        public Reminder() : this(ReminderSettings.Create(Context.System.Settings.Config.GetConfig("akka.persistence.reminder")))
        {
        }

        public Reminder(ReminderSettings settings)
        {
            this.UpdateState = e =>
            {
                switch (e)
                {
                    case Scheduled scheduled:
                        this.state = state.AddEntry(scheduled.Entry);
                        break;
                    case Completed completed:
                        this.state = state.RemoveEntry(completed.TaskId);
                        break;
                }
            };

            this.settings = settings;
            PersistenceId = settings.PersistenceId;
            JournalPluginId = settings.JournalPluginId;
            SnapshotPluginId = settings.SnapshotPluginId;

            tickTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(settings.TickInterval, settings.TickInterval, Self, Tick.Instance, ActorRefs.NoSender);

            Command<Tick>(_ =>
            {
                var now = DateTime.UtcNow;
                foreach (var schedule in state.Entries.Values.Where(e => ShouldTrigger(e, now)))
                {
                    Log.Info("Sending message [{0}] to recipient [{1}]", schedule.Message, schedule.Recipient);

                    var selection = Context.ActorSelection(schedule.Recipient);
                    selection.Tell(schedule.Message, ActorRefs.NoSender);

                    Emit(new Completed(schedule.TaskId, now), UpdateState);

                    var next = schedule.WithNextTriggerDate(now);
                    if (next != null)
                    {
                        Emit(new Scheduled(next), UpdateState);
                    }
                }
            });
            Command<Schedule>(schedule =>
            {
                var sender = Sender;
                try
                {
                    Emit(new Scheduled(schedule.WithoutAck()), e =>
                    {
                        UpdateState(e);
                        //NOTE: use `schedule`, not `e` - latter contains a version with ACK explicitly erased to avoid storing ack in persistent memory
                        if (schedule.Ack != null)
                        {
                            sender.Tell(schedule.Ack);
                        }
                    });
                }
                catch (Exception error)
                {
                    Log.Error(error, "Failed to schedule: [{0}]", schedule);
                    if (schedule.Ack != null)
                    {
                        sender.Tell(new Status.Failure(error));
                    }
                }
            });
            Command<GetState>(_ =>
            {
                Sender.Tell(state);
            });
            Command<Cancel>(cancel =>
            {
                var sender = Sender;
                try
                {
                    Emit(new Completed(cancel.TaskId, DateTime.UtcNow), e =>
                    {
                        UpdateState(e);
                        if (cancel.Ack != null)
                        {
                            sender.Tell(cancel.Ack);
                        }
                    });
                }
                catch (Exception error)
                {
                    Log.Error(error, "Failed to cancel task: [{0}]", cancel.TaskId);
                    if (cancel.Ack != null)
                    {
                        sender.Tell(new Status.Failure(error));
                    }
                }
            });
            Command<SaveSnapshotSuccess>(success =>
            {
                Log.Debug("Successfully saved reminder snapshot. Removing all events before seqNr [{0}]", success.Metadata.SequenceNr);
                DeleteMessages(success.Metadata.SequenceNr - 1);
            });
            Command<SaveSnapshotFailure>(failure =>
            {
                Log.Error(failure.Cause, "Failed to save reminder snapshot");
            });
            Recover<Scheduled>(UpdateState);
            Recover<Completed>(UpdateState);
            Recover<SnapshotOffer>(offer =>
            {
                if (offer.Snapshot is State state)
                {
                    this.state = state;
                }
            });
        }

        protected virtual bool ShouldTrigger(Schedule schedule, DateTime now)
        {
            return schedule.TriggerDateUtc <= now;
        }

        private void Emit<T>(T reminderEvent, Action<T> handler) where T : IReminderEvent
        {
            PersistAsync(reminderEvent, e =>
            {
                handler(e);
                SaveSnapshotIfNeeded();
            });
        }

        private void SaveSnapshotIfNeeded()
        {
            counter = (counter + 1) % settings.SnapshotInterval;
            if (counter == 0)
            {
                SaveSnapshot(state);
            }
        }

        protected override void PostStop()
        {
            tickTask.Cancel();
            base.PostStop();
        }
    }
}