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
        private int counter = 0;
        private readonly ReminderSettings settings;

        public Reminder() : this(ReminderSettings.Create(Context.System.Settings.Config.GetConfig("akka.persistence.reminder")))
        {
        }

        public Reminder(ReminderSettings settings)
        {
            this.settings = settings;
            PersistenceId = settings.PersistenceId;
            JournalPluginId = settings.JournalPluginId;
            SnapshotPluginId = settings.SnapshotPluginId;

            tickTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(settings.TickInterval, settings.TickInterval, Self, Tick.Instance, ActorRefs.NoSender);

            Command<Tick>(_ =>
            {
                var now = DateTime.UtcNow;
                foreach (var entry in state.Entries.Values.Where(e => ShouldTrigger(e, now)))
                {
                    Log.Info("Sending message [{0}] to recipient [{1}]", entry.Message, entry.Recipient);

                    var selection = Context.ActorSelection(entry.Recipient);
                    selection.Tell(entry.Message, ActorRefs.NoSender);

                    Emit(new Completed(entry.TaskId, now), UpdateState);

                    if (entry.RepeatInterval.HasValue)
                    {
                        var next = now + entry.RepeatInterval.Value;
                        Emit(new Scheduled(entry.WithNextTriggerDate(next)), UpdateState);
                    }
                }
            });
            Command<Schedule>(schedule =>
            {
                var entry = new Entry(schedule.TaskId, schedule.Recipient, schedule.Message, schedule.TriggerDateUtc, schedule.RepeatInterval);
                var sender = Sender;
                Emit(new Scheduled(entry), e =>
                {
                    UpdateState(e);
                    if (schedule.Ack != null)
                    {
                        sender.Tell(schedule.Ack);
                    }
                });
            });
            Command<GetState>(_ =>
            {
                Sender.Tell(state);
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
            Recover<Scheduled>(scheduled => UpdateState(scheduled));
            Recover<Completed>(completed => UpdateState(completed));
            Recover<SnapshotOffer>(offer =>
            {
                if (offer.Snapshot is State offerred)
                {
                    state = offerred;
                }
            });
        }

        protected virtual bool ShouldTrigger(Entry entry, DateTime now)
        {
            return entry.TriggerDateUtc <= now;
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

        private void UpdateState(IReminderEvent e)
        {
            switch (e)
            {
                case Scheduled scheduled:
                    state = state.AddEntry(scheduled.Entry);
                    break;
                case Completed completed:
                    state = state.RemoveEntry(completed.TaskId);
                    break;
            }

            Log.Debug("Reminder state has been updated from the event: {0}", e);
        }

        protected override void PostStop()
        {
            tickTask.Cancel();
            base.PostStop();
        }
    }
}