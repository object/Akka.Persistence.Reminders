#region copyright
// -----------------------------------------------------------------------
//  <copyright file="ReminderSettings.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017 Bartosz Sypytkowski <b.sypytkowski@gmail.com>
//  </copyright>
// -----------------------------------------------------------------------
#endregion

using System;
using Akka.Configuration;

namespace Akka.Persistence.Reminders
{
    /// <summary>
    /// An immutable statically-typed settings class used by <see cref="Reminder"/> actor.
    /// </summary>
    public class ReminderSettings
    {
        /// <summary>
        /// An instance of a <see cref="ReminderSettings"/> with default configuration.
        /// </summary>
        public static ReminderSettings Default { get; } = new ReminderSettings(
            persistenceId: "reminder",
            journalPluginId: "",
            snapshotPluginId: "",
            tickInterval: TimeSpan.FromSeconds(10),
            snapshotInterval: 100);

        public static ReminderSettings Create(Config config)
        {
            if (config == null) return Default;

            return new ReminderSettings(
                persistenceId: config.GetString("persistence-id", "reminder"),
                journalPluginId: config.GetString("journal-plugin-id", ""),
                snapshotPluginId: config.GetString("snapshot-plugin-id", ""),
                tickInterval: config.GetTimeSpan("tick-inverval", TimeSpan.FromSeconds(10)),
                snapshotInterval: config.GetInt("snapshot-interval", 100));
        }

        /// <summary>
        /// Persistent identifier for event stream produced by correlated reminder. 
        /// Default: "reminder".
        /// </summary>
        public string PersistenceId { get; }

        /// <summary>
        /// Identifier of a event journal used by correlated reminder. 
        /// Default: akka.persistence default.
        /// </summary>
        public string JournalPluginId { get; }

        /// <summary>
        /// Identifer of a snapshot store used by correlated reminder. 
        /// Default: akka.persistence default.
        /// </summary>
        public string SnapshotPluginId { get; }

        /// <summary>
        /// Unlike standard akka.net scheduler, reminders work in much lower frequency.
        /// Reason for this is that they are designed for a long running tasks (think of
        /// minutes, hours, days or weeks). Default: 10 seconds.
        /// </summary>
        public TimeSpan TickInterval { get; }

        /// <summary>
        /// Reminder uses standard akka.persistence eventsourcing for maintaining scheduler
        /// internal state. In order to make a stream of events shorter (and ultimately 
        /// allowing for a faster recovery in case of failure), a reminder state snapshot is
        /// performed after a series of consecutive events have been stored.
        /// Default: every 100 consecutively persisted events.
        /// </summary>
        public int SnapshotInterval { get; }

        public ReminderSettings(string persistenceId, string journalPluginId, string snapshotPluginId, TimeSpan tickInterval, int snapshotInterval)
        {
            PersistenceId = persistenceId ?? throw new ArgumentNullException(nameof(persistenceId));
            JournalPluginId = journalPluginId ?? throw new ArgumentNullException(nameof(journalPluginId));
            SnapshotPluginId = snapshotPluginId ?? throw new ArgumentNullException(nameof(snapshotPluginId));
            TickInterval = tickInterval;
            SnapshotInterval = snapshotInterval;
        }

        /// <summary>
        /// Returns a new instance of <see cref="ReminderSettings"/> with overriden <see cref="PersistenceId"/>.
        /// </summary>
        public ReminderSettings WithPersistenceId(string persistenceId) => Copy(persistenceId: persistenceId);

        /// <summary>
        /// Returns a new instance of <see cref="ReminderSettings"/> with overriden <see cref="JournalPluginId"/>.
        /// In order to set it to default value, use empty string.
        /// </summary>
        public ReminderSettings WithJournalPluginId(string journalPluginId) => Copy(journalPluginId: journalPluginId);

        /// <summary>
        /// Returns a new instance of <see cref="ReminderSettings"/> with overriden <see cref="SnapshotPluginId"/>.
        /// In order to set it to default value, use empty string.
        /// </summary>
        public ReminderSettings WithSnapshotPluginId(string snapshotPluginId) => Copy(snapshotPluginId: snapshotPluginId);

        /// <summary>
        /// Returns a new instance of <see cref="ReminderSettings"/> with overriden <see cref="TickInterval"/>.
        /// </summary>
        public ReminderSettings WithTickInterval(TimeSpan tickInterval) => Copy(tickInterval: tickInterval);

        /// <summary>
        /// Returns a new instance of <see cref="ReminderSettings"/> with overriden <see cref="SnapshotInterval"/>.
        /// </summary>
        public ReminderSettings WithSnapshotInterval(int snapshotInterval) => Copy(snapshotInterval: snapshotInterval);

        private ReminderSettings Copy(string persistenceId = null, string journalPluginId = null, string snapshotPluginId = null, TimeSpan? tickInterval = null, int? snapshotInterval = null) =>
            new ReminderSettings(
                persistenceId: persistenceId ?? PersistenceId,
                journalPluginId: journalPluginId ?? JournalPluginId,
                snapshotPluginId: snapshotPluginId ?? SnapshotPluginId,
                tickInterval: tickInterval ?? TickInterval,
                snapshotInterval: snapshotInterval ?? SnapshotInterval);
    }
}