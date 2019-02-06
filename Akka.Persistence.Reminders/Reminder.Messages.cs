#region copyright
// -----------------------------------------------------------------------
//  <copyright file="Reminder.Messages.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Akka.Actor;
using Cronos;

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
        /// An immutable version of a <see cref="Reminder"/> actor state, containing 
        /// information about all currently scheduled tasks waiting to be executed.
        /// </summary>
        public sealed class State : IEquatable<State>, IReminderFormat
        {
            public static State Empty { get; } = new State(ImmutableDictionary<string, Schedule>.Empty);

            public State(ImmutableDictionary<string, Schedule> entries)
            {
                Entries = entries;
            }

            public ImmutableDictionary<string, Schedule> Entries { get; }

            public State AddEntry(Schedule entry) => new State(Entries.SetItem(entry.TaskId, entry));
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
        /// Optionally user may specify <see cref="Ack"/> which, when defined, will
        /// be send back to <see cref="Schedule"/> message sender, when the task has been confirmed
        /// as correctly persisted.
        ///
        /// Other scheduling options include <see cref="ScheduleRepeatedly"/> and <see cref="ScheduleCron"/>.
        /// </summary>
        /// <seealso cref="ScheduleRepeatedly"/>
        /// <seealso cref="ScheduleCron"/>
        public class Schedule : IReminderCommand, IEquatable<Schedule>, IReminderFormat
        {
            public string TaskId { get; }
            public ActorPath Recipient { get; }
            public object Message { get; }
            public DateTime TriggerDateUtc { get; }
            public object Ack { get; }

            public Schedule(string taskId, ActorPath recipient, object message, DateTime triggerDateUtc, object ack = null)
            {
                TaskId = taskId ?? throw new ArgumentNullException(nameof(taskId));
                Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient));
                Message = message ?? throw new ArgumentNullException(nameof(message));
                TriggerDateUtc = triggerDateUtc;
                Ack = ack;
            }

            public virtual Schedule WithNextTriggerDate(DateTime utcDate) => null;

            public virtual Schedule WithoutAck() => new Schedule(TaskId, Recipient, Message, TriggerDateUtc);

            public bool Equals(Schedule other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                if (this.GetType() != other.GetType()) return false;
                return string.Equals(TaskId, other.TaskId) && Equals(Recipient, other.Recipient) && Equals(Message, other.Message) && TriggerDateUtc.Equals(other.TriggerDateUtc) && Equals(Ack, other.Ack);
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
                    hashCode = (hashCode * 397) ^ (Ack != null ? Ack.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public override string ToString() => $"Schedule(id:'{TaskId}', receiver:{Recipient}, at:{TriggerDateUtc}, message:{Message})";
        }
        
        /// <summary>
        /// A request send to an instance of a <see cref="Reminder"/> actor, ordering
        /// it to schedule a <see cref="Schedule.Message"/> to be send to a provided <see cref="Schedule.Recipient"/>
        /// at time of <see cref="Schedule.TriggerDateUtc"/>.
        /// 
        /// A <see cref="RepeatInterval"/> may be provided if this message should be 
        /// resend in repeating time window from a given <see cref="Schedule.TriggerDateUtc"/>.
        /// 
        /// Optionally user may specify <see cref="Schedule.Ack"/> which, when defined, will
        /// be send back to <see cref="Schedule"/> message sender, when the task has been confirmed
        /// as correctly persisted.
        /// </summary>
        /// <seealso cref="Schedule"/>
        /// <seealso cref="ScheduleCron"/>
        public sealed class ScheduleRepeatedly : Schedule, IEquatable<ScheduleRepeatedly>
        {
            public TimeSpan RepeatInterval { get; }

            public ScheduleRepeatedly(string taskId, ActorPath recipient, object message, DateTime triggerDateUtc, TimeSpan repeatInterval, object ack = null)
                : base(taskId, recipient, message, triggerDateUtc, ack)
            {
                RepeatInterval = repeatInterval;
            }
            
            public override Schedule WithNextTriggerDate(DateTime utcDate) => new ScheduleRepeatedly(TaskId, Recipient, Message, utcDate + RepeatInterval, RepeatInterval);

            public override Schedule WithoutAck() => new ScheduleRepeatedly(TaskId, Recipient, Message, TriggerDateUtc, RepeatInterval);

            public bool Equals(ScheduleRepeatedly other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;

                return string.Equals(TaskId, other.TaskId) 
                       && Equals(Recipient, other.Recipient) 
                       && Equals(Message, other.Message) 
                       && TriggerDateUtc.Equals(other.TriggerDateUtc) 
                       && Equals(Ack, other.Ack)
                       && RepeatInterval == other.RepeatInterval;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is ScheduleRepeatedly && Equals((ScheduleRepeatedly)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = TaskId.GetHashCode();
                    hashCode = (hashCode * 397) ^ Recipient.GetHashCode();
                    hashCode = (hashCode * 397) ^ Message.GetHashCode();
                    hashCode = (hashCode * 397) ^ TriggerDateUtc.GetHashCode();
                    hashCode = (hashCode * 397) ^ (Ack != null ? Ack.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ RepeatInterval.GetHashCode();
                    return hashCode;
                }
            }


            public override string ToString() => $"Schedule(id:'{TaskId}', receiver:{Recipient}, at:{TriggerDateUtc}, every: {RepeatInterval}, message:{Message})";
        }

        /// <summary>
        /// A request send to an instance of a <see cref="Reminder"/> actor, ordering
        /// it to schedule a <see cref="Schedule.Message"/> to be send to a provided <see cref="Schedule.Recipient"/>
        /// at time of <see cref="Schedule.TriggerDateUtc"/>.
        /// 
        /// A <see cref="CronExpression"/> may be provided if this message should be 
        /// resend using a rule set specified by cron expression (see: https://en.wikipedia.org/wiki/Cron).
        /// 
        /// Optionally user may specify <see cref="Schedule.Ack"/> which, when defined, will
        /// be send back to <see cref="Schedule"/> message sender, when the task has been confirmed
        /// as correctly persisted.
        /// </summary>
        /// <seealso cref="Schedule"/>
        /// <seealso cref="ScheduleRepeatedly"/>
        public sealed class ScheduleCron : Schedule, IEquatable<ScheduleCron>
        {
            public string CronExpression { get; }

            private readonly CronExpression Expression;

            private ScheduleCron(string taskId, ActorPath recipient, object message, DateTime triggerDateUtc, string cronExpression, CronExpression expr, object ack = null)
                : base(taskId, recipient, message, triggerDateUtc, ack)
            {
                // we still need to keep this thing around, see: https://github.com/HangfireIO/Cronos/issues/15
                CronExpression = cronExpression;
                Expression = Cronos.CronExpression.Parse(cronExpression);
            }

            public ScheduleCron(string taskId, ActorPath recipient, object message, DateTime triggerDateUtc, string cronExpression, object ack = null)
                : this(taskId, recipient, message, triggerDateUtc, cronExpression, Cronos.CronExpression.Parse(cronExpression), ack)
            {
            }

            public override Schedule WithNextTriggerDate(DateTime utcDate)
            {
                var next = Expression.GetNextOccurrence(utcDate);
                if (next.HasValue)
                    return new ScheduleCron(TaskId, Recipient, Message, next.Value, CronExpression, Expression);
                else
                    return null;
            }

            public override Schedule WithoutAck() => new ScheduleCron(TaskId, Recipient, Message, TriggerDateUtc, CronExpression, Expression);

            public bool Equals(ScheduleCron other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;

                return string.Equals(TaskId, other.TaskId)
                       && Equals(Recipient, other.Recipient)
                       && Equals(Message, other.Message)
                       && TriggerDateUtc.Equals(other.TriggerDateUtc)
                       && Equals(Ack, other.Ack)
                       && CronExpression == other.CronExpression;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is ScheduleCron && Equals((ScheduleCron)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = TaskId.GetHashCode();
                    hashCode = (hashCode * 397) ^ Recipient.GetHashCode();
                    hashCode = (hashCode * 397) ^ Message.GetHashCode();
                    hashCode = (hashCode * 397) ^ TriggerDateUtc.GetHashCode();
                    hashCode = (hashCode * 397) ^ (Ack != null ? Ack.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ Expression.GetHashCode();
                    return hashCode;
                }
            }


            public override string ToString() => $"Schedule(id:'{TaskId}', receiver:{Recipient}, at:{TriggerDateUtc}, cron: '{CronExpression}', message:{Message})";
        }

        public sealed class Scheduled : IReminderEvent, IEquatable<Scheduled>, IReminderFormat
        {
            public Scheduled(Schedule entry)
            {
                Entry = entry;
            }

            public Schedule Entry { get; }

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

            public override string ToString() => $"Cancel(taskId: {TaskId})";
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