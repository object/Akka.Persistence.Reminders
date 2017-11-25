#region copyright
// -----------------------------------------------------------------------
//  <copyright file="Reminder.Messages.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Immutable;
using Akka.Actor;

namespace Akka.Persistence.Reminders
{
    public interface IReminderFormat { }
    public interface IReminderMessage { }
    public interface IReminderCommand : IReminderMessage { }
    public interface IReminderEvent : IReminderMessage { }

    public partial class Reminder
    {
        #region internal classes

        /// <summary>
        /// An entry represents a single task scheduled by <see cref="Reminder"/> actor.
        /// </summary>
        public sealed class Entry : IEquatable<Entry>, IReminderFormat
        {
            public string TaskId { get; }
            public ActorPath Recipient { get; }
            public object Message { get; }
            public DateTime TriggerDateUtc { get; }
            public TimeSpan? RepeatInterval { get; }

            public Entry(string taskId, ActorPath receiver, object message, DateTime triggerDateUtc, TimeSpan? repeatInterval = null)
            {
                TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
                Recipient = receiver ?? throw new ArgumentNullException(nameof(receiver));
                Message = message ?? throw new ArgumentNullException(nameof(message));
                TriggerDateUtc = triggerDateUtc;
                RepeatInterval = repeatInterval;
            }

            public Entry WithNextTriggerDate(DateTime nextDate) => new Entry(
                taskId: TaskId,
                receiver: Recipient,
                message: Message,
                triggerDateUtc: nextDate,
                repeatInterval: RepeatInterval);

            public bool Equals(Entry other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return TaskId.Equals(other.TaskId) 
                    && Equals(Recipient, other.Recipient) 
                    && Equals(Message, other.Message) 
                    && TriggerDateUtc.Equals(other.TriggerDateUtc) 
                    && RepeatInterval.Equals(other.RepeatInterval);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is Entry && Equals((Entry)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = TaskId.GetHashCode();
                    hashCode = (hashCode * 397) ^ Recipient.GetHashCode();
                    hashCode = (hashCode * 397) ^ Message.GetHashCode();
                    hashCode = (hashCode * 397) ^ TriggerDateUtc.GetHashCode();
                    hashCode = (hashCode * 397) ^ RepeatInterval.GetHashCode();
                    return hashCode;
                }
            }

            public override string ToString() => $"Entry(id:{TaskId}, receiver:{Recipient}, at:{TriggerDateUtc}, repeat:{RepeatInterval?.ToString() ?? "no"}, message:{Message})";
        }

        /// <summary>
        /// An immutable version of a <see cref="Reminder"/> actor state, containing 
        /// information about all currently scheduled tasks waiting to be executed.
        /// </summary>
        public sealed class State : IEquatable<State>, IReminderFormat
        {
            public static State Empty { get; } = new State(ImmutableDictionary<string, Entry>.Empty);

            public State(ImmutableDictionary<string, Entry> entries)
            {
                Entries = entries;
            }

            public ImmutableDictionary<string, Entry> Entries { get; }

            public State AddEntry(Entry entry) => new State(Entries.SetItem(entry.TaskId, entry));
            public State RemoveEntry(string taskId) => new State(Entries.Remove(taskId));

            public bool Equals(State other)
            {
                if (ReferenceEquals(other, null)) return false;
                if (ReferenceEquals(this, other)) return true;

                if (Entries.Count != other.Entries.Count) return false;
                foreach (var kv in Entries)
                {
                    var thisEntry = kv.Value;
                    if (!other.Entries.TryGetValue(kv.Key, out var otherEntry)
                        || !Equals(thisEntry, otherEntry)) return false;
                }

                return true;
            }

            public override bool Equals(object obj) => obj is State state && Equals(state);

            public override int GetHashCode()
            {
                var hash = 0;
                foreach (var entry in Entries.Values)
                {
                    unchecked
                    {
                        hash ^= (397 * entry.GetHashCode());
                    }
                }
                return hash;
            }
        }


        /// <summary>
        /// A request send to an instance of a <see cref="Reminder"/> actor, ordering
        /// it to schedule a <see cref="Message"/> to be send to a provided <see cref="Recipient"/>
        /// at time of <see cref="TriggerDateUtc"/>.
        /// 
        /// Optionally a <see cref="RepeatInterval"/> may be provided if this message should be 
        /// resend in repeating time window from a given <see cref="TriggerDateUtc"/>.
        /// 
        /// Optinally user may specify <see cref="Ack"/>nowledgement which, when defined, will
        /// be send back to <see cref="Schedule"/> message sender, when the task has been confirmed
        /// as correctly persisted.
        /// </summary>
        public sealed class Schedule : IReminderCommand, IEquatable<Schedule>, IReminderFormat
        {
            public string TaskId { get; }
            public ActorPath Recipient { get; }
            public object Message { get; }
            public DateTime TriggerDateUtc { get; }
            public TimeSpan? RepeatInterval { get; }
            public object Ack { get; }

            public Schedule(string taskId, ActorPath receiver, object message, DateTime triggerDateUtc, TimeSpan? repeatInterval = null, object ack = null)
            {
                TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
                Recipient = receiver ?? throw new ArgumentNullException(nameof(receiver));
                Message = message ?? throw new ArgumentNullException(nameof(message));
                TriggerDateUtc = triggerDateUtc;
                Ack = ack;
                RepeatInterval = repeatInterval;
            }

            public bool Equals(Schedule other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(TaskId, other.TaskId) && Equals(Recipient, other.Recipient) && Equals(Message, other.Message) && TriggerDateUtc.Equals(other.TriggerDateUtc) && RepeatInterval.Equals(other.RepeatInterval) && Equals(Ack, other.Ack);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is Schedule && Equals((Schedule)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = TaskId.GetHashCode();
                    hashCode = (hashCode * 397) ^ Recipient.GetHashCode();
                    hashCode = (hashCode * 397) ^ Message.GetHashCode();
                    hashCode = (hashCode * 397) ^ TriggerDateUtc.GetHashCode();
                    hashCode = (hashCode * 397) ^ RepeatInterval.GetHashCode();
                    hashCode = (hashCode * 397) ^ (Ack != null ? Ack.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public override string ToString() => $"Schedule(id:{TaskId}, receiver:{Recipient}, at:{TriggerDateUtc}, repeat:{RepeatInterval?.ToString() ?? "no"}, message:{Message})";
        }

        public sealed class Scheduled : IReminderEvent, IEquatable<Scheduled>, IReminderFormat
        {
            public Scheduled(Entry entry)
            {
                Entry = entry;
            }

            public Entry Entry { get; }

            public bool Equals(Scheduled other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return Equals(Entry, other.Entry);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is Scheduled && Equals((Scheduled)obj);
            }

            public override int GetHashCode()
            {
                return (Entry != null ? Entry.GetHashCode() : 0);
            }

            public override string ToString() => $"Scheduled({Entry})";
        }

        public sealed class Completed : IReminderEvent, IEquatable<Completed>, IReminderFormat
        {
            public string TaskId { get; }
            public DateTime TriggerDateUtc { get; }

            public Completed(string taskId, DateTime triggerDateUtc)
            {
                TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
                TriggerDateUtc = triggerDateUtc;
            }

            public bool Equals(Completed other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return TaskId.Equals(other.TaskId) && TriggerDateUtc.Equals(other.TriggerDateUtc);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is Completed && Equals((Completed)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (TaskId.GetHashCode() * 397) ^ TriggerDateUtc.GetHashCode();
                }
            }

            public override string ToString() => $"Completed(id:{TaskId}, at:{TriggerDateUtc})";
        }

        /// <summary>
        /// Cancels a previous <see cref="Schedule"/> identified by <see cref="TaskId"/>.
        /// 
        /// Optionally, if <see cref="Ack"/> has been defined it will be returned back to cancel 
        /// sender, when cancellation has been completed.
        /// </summary>
        public sealed class Cancel : IReminderCommand, IEquatable<Cancel>, IReminderFormat
        {
            public Cancel(string taskId, object ack = null)
            {
                TaskId = taskId;
                Ack = ack;
            }

            public string TaskId { get; }
            public object Ack { get; }

            public bool Equals(Cancel other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(TaskId, other.TaskId) && Equals(Ack, other.Ack);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is Cancel && Equals((Cancel) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((TaskId != null ? TaskId.GetHashCode() : 0) * 397) ^ (Ack != null ? Ack.GetHashCode() : 0);
                }
            }
        }

        /// <summary>
        /// A <see cref="Reminder"/> actor request used to retrieve information about currently
        /// scheduled entries. In reply an actor will send a <see cref="State"/> object back to
        /// a message sender, containing the latest known information about reminder state.
        /// </summary>
        public sealed class GetState : IReminderCommand, IReminderFormat
        {
            public static GetState Instance { get; } = new GetState();
            private GetState() { }
        }

        internal sealed class Tick : IReminderCommand
        {
            public static Tick Instance { get; } = new Tick();
            private Tick() { }
        }

        #endregion
    }
}