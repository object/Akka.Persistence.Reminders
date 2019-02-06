#region copyright
// -----------------------------------------------------------------------
//  <copyright file="ReminderSerializer.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using System.Collections.Immutable;
using Akka.Actor;
using Akka.Persistence.Reminders.Serialization.Proto;
using Akka.Serialization;
using Akka.Util;
using Cronos;
using Google.Protobuf;

namespace Akka.Persistence.Reminders.Serialization
{
    public sealed class ReminderSerializer : SerializerWithStringManifest
    {
        public const string StateManifest = "A";
        public const string EntryManifest = "B";
        public const string CompletedManifest = "C";
        public const string ScheduleManifest = "D";
        public const string ScheduledManifest = "E";
        public const string GetStateManifest = "F";
        public const string CancelManifest = "G";

        public static readonly byte[] EmptyBytes = new byte[0];

        private readonly ExtendedActorSystem _system;
        protected Akka.Serialization.Serialization Serialization => _system.Serialization;

        public ReminderSerializer(ExtendedActorSystem system) : base(system)
        {
            _system = system;
        }

        public override byte[] ToBinary(object obj)
        {
            switch (obj)
            {
                case Reminder.State state: return StateToProto(state).ToByteArray();
                case Reminder.Completed completed: return CompletedToProto(completed).ToByteArray();
                case Reminder.Schedule schedule: return ScheduleToProto(schedule).ToByteArray();
                case Reminder.Scheduled scheduled: return ScheduledToProto(scheduled).ToByteArray();
                case Reminder.Cancel cancel: return CancelToProto(cancel).ToByteArray();
                case Reminder.GetState _: return EmptyBytes;
                default: throw new ArgumentException($"'{nameof(ReminderSerializer)}' doesn't support serialization of '{obj.GetType()}'");
            }
        }

        private Proto.ReminderCancel CancelToProto(Reminder.Cancel cancel)
        {
            var proto = new ReminderCancel
            {
                TaskId = cancel.TaskId
            };

            if (cancel.Ack != null)
                proto.Ack = MessageToProto(cancel.Ack);

            return proto;
        }

        private Proto.ReminderScheduled ScheduledToProto(Reminder.Scheduled scheduled)
        {
            return new ReminderScheduled
            {
                Entry = EntryToProto(scheduled.Entry)
            };
        }

        private Proto.ReminderSchedule ScheduleToProto(Reminder.Schedule schedule)
        {
            var proto = new ReminderSchedule
            {
                TaskId = schedule.TaskId,
                Recipient = ActorPathToProto(schedule.Recipient),
                Payload = MessageToProto(schedule.Message),
                TriggerDate = schedule.TriggerDateUtc.Ticks
            };

            switch (schedule)
            {
                case Reminder.ScheduleRepeatedly repeat:
                    proto.RepeatInterval = repeat.RepeatInterval.Ticks;
                    break;
                case Reminder.ScheduleCron cron:
                    proto.CronExpression = cron.CronExpression.ToString();
                    break;
            }

            if (schedule.Ack != null)
                proto.Ack = MessageToProto(schedule.Ack);

            return proto;
        }

        private Proto.ReminderCompleted CompletedToProto(Reminder.Completed completed)
        {
            return new ReminderCompleted
            {
                TaskId = completed.TaskId,
                CompletionDate = completed.TriggerDateUtc.Ticks
            };
        }

        [Obsolete("Entries have been fully replaced by Schedule. This is only for compatibility reasons.")]
        private Proto.ReminderEntry EntryToProto(Reminder.Schedule schedule)
        {
            var proto = new Proto.ReminderEntry
            {
                TaskId = schedule.TaskId,
                Recipient = ActorPathToProto(schedule.Recipient),
                Payload = MessageToProto(schedule.Message),
                TriggerDate = schedule.TriggerDateUtc.Ticks
            };

            switch (schedule)
            {
                case Reminder.ScheduleRepeatedly repeat:
                    proto.RepeatInterval = repeat.RepeatInterval.Ticks;
                    break;
                case Reminder.ScheduleCron cron:
                    proto.CronExpression = cron.CronExpression;
                    break;
            }
            return proto;
        }

        private OtherMessage MessageToProto(object message)
        {
            var serializer = Serialization.FindSerializerFor(message);
            var proto = new OtherMessage
            {
                SerializerId = serializer.Identifier,
                Body = ByteString.CopyFrom(serializer.ToBinary(message))
            };

            if (serializer is SerializerWithStringManifest withManifest)
            {
                proto.Manifest = withManifest.Manifest(message);
            }
            else if (serializer.IncludeManifest)
            {
                proto.Manifest = message.GetType().TypeQualifiedName();
            }

            return proto;
        }

        private string ActorPathToProto(ActorPath actorPath)
        {
            return actorPath.ToStringWithAddress();
        }

        private Proto.ReminderState StateToProto(Reminder.State state)
        {
            var proto = new Proto.ReminderState();
            foreach (var schedule in state.Entries.Values)
            {
                proto.Entries.Add(EntryToProto(schedule));
            }
            return proto;
        }

        public override object FromBinary(byte[] bytes, string manifest)
        {
            switch (manifest)
            {
                case EntryManifest: return EntryFromProto(Proto.ReminderEntry.Parser.ParseFrom(bytes));
                case ScheduleManifest: return ScheduleFromProto(Proto.ReminderSchedule.Parser.ParseFrom(bytes));
                case StateManifest: return StateFromProto(Proto.ReminderState.Parser.ParseFrom(bytes));
                case CompletedManifest: return CompletedFromProto(Proto.ReminderCompleted.Parser.ParseFrom(bytes));
                case ScheduledManifest: return ScheduledFromProto(Proto.ReminderScheduled.Parser.ParseFrom(bytes));
                case GetStateManifest: return Reminder.GetState.Instance;
                case CancelManifest: return CancelFromProto(Proto.ReminderCancel.Parser.ParseFrom(bytes));
                default: throw new ArgumentException($"'{nameof(ReminderSerializer)}' doesn't support serialization of unknown manifest '{manifest}'");
            }
        }

        private Reminder.Cancel CancelFromProto(ReminderCancel proto)
        {
            var ack = proto.Ack != null ? MessageFromProto(proto.Ack) : null;
            return new Reminder.Cancel(proto.TaskId, ack);
        }

        private Reminder.State StateFromProto(Proto.ReminderState proto)
        {
            var builder = ImmutableDictionary<string, Reminder.Schedule>.Empty.ToBuilder();
            foreach (var protoEntry in proto.Entries)
            {
                var taskId = protoEntry.TaskId;
                builder[taskId] = EntryFromProto(protoEntry);
            }

            return new Reminder.State(builder.ToImmutable());
        }

        [Obsolete("Entries have been fully replaced by Schedule. This is only for compatibility reasons.")]
        private Reminder.Schedule EntryFromProto(ReminderEntry proto)
        {
            var recipient = ActorPathFromProto(proto.Recipient);
            var message = MessageFromProto(proto.Payload);

            if (!string.IsNullOrEmpty(proto.CronExpression))
            {
                return new Reminder.ScheduleCron(proto.TaskId, recipient, message, new DateTime(proto.TriggerDate), proto.CronExpression);
            }
            else if (proto.RepeatInterval != 0)
            {
                var repeat = new TimeSpan(proto.RepeatInterval);
                return new Reminder.ScheduleRepeatedly(proto.TaskId, recipient, message, new DateTime(proto.TriggerDate), repeat);
            }
            else
            {
                return new Reminder.Schedule(proto.TaskId, recipient, message, new DateTime(proto.TriggerDate));
            }
        }

        private Reminder.Completed CompletedFromProto(ReminderCompleted proto)
        {
            return new Reminder.Completed(proto.TaskId, new DateTime(proto.CompletionDate));
        }

        private Reminder.Schedule ScheduleFromProto(ReminderSchedule proto)
        {
            var ack = proto.Ack != null
                ? MessageFromProto(proto.Ack)
                : null;
            var recipient = ActorPathFromProto(proto.Recipient);
            var message = MessageFromProto(proto.Payload);

            if (!string.IsNullOrEmpty(proto.CronExpression))
            {
                return new Reminder.ScheduleCron(proto.TaskId, recipient, message, new DateTime(proto.TriggerDate), proto.CronExpression, ack);
            }
            else if (proto.RepeatInterval != 0)
            {
                var repeat = new TimeSpan(proto.RepeatInterval);
                return new Reminder.ScheduleRepeatedly(proto.TaskId, recipient, message, new DateTime(proto.TriggerDate), repeat, ack);
            }
            else
            {
                return new Reminder.Schedule(proto.TaskId, recipient, message, new DateTime(proto.TriggerDate), ack);
            }
        }

        private object MessageFromProto(OtherMessage proto)
        {
            return Serialization.Deserialize(proto.Body.ToByteArray(), proto.SerializerId, proto.Manifest);
        }

        private ActorPath ActorPathFromProto(string proto)
        {
            return ActorPath.TryParse(proto, out var path) ? path : system.DeadLetters.Path;
        }

        private Reminder.Scheduled ScheduledFromProto(ReminderScheduled proto)
        {
            return new Reminder.Scheduled(EntryFromProto(proto.Entry));
        }

        public override string Manifest(object o)
        {
            switch (o)
            {
                case Reminder.State _: return StateManifest;
                case Reminder.Completed _: return CompletedManifest;
                case Reminder.Schedule _: return ScheduleManifest;
                case Reminder.Scheduled _: return ScheduledManifest;
                case Reminder.GetState _: return GetStateManifest;
                case Reminder.Cancel _: return CancelManifest;
                default: throw new ArgumentException($"'{nameof(ReminderSerializer)}' doesn't support serialization of '{o.GetType()}'");
            }
        }
    }
}