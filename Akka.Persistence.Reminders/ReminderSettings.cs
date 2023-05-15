#region copyright
// -----------------------------------------------------------------------
//  <copyright file="ReminderSettings.cs" creator="Bartosz Sypytkowski">
//      Copyright (C) 2017-2023 Bartosz Sypytkowski and contributors
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
            snapshotInterval: 100,
            cleanupOldMessages: true,
            cleanupOldSnapshots: false
            );

        public static ReminderSettings Create(Config config)
        {
            if (config == null) return Default;

            return new ReminderSettings(
                persistenceId: config.GetString("persistence-id", "reminder"),
                journalPluginId: config.GetString("journal-plugin-id", ""),
                snapshotPluginId: config.GetString("snapshot-plugin-id", ""),
                tickInterval: config.GetTimeSpan("tick-inverval", TimeSpan.FromSeconds(10)),
                snapshotInterval: config.GetInt("snapshot-interval", 100),
                cleanupOldMessages: config.GetBoolean("cleanup-old-messages", true),
                cleanupOldSnapshots: config.GetBoolean("cleanup-old-snapshots", false));
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

        /// <summary>
        /// Reminder periodically saves its state in Snapshot store, so old EventJournal entries can be removed from the database.
        /// Default: true. (Default is set to true to match Reminder's behavior when this settings was not configurable)
        /// </summary>
        public bool CleanupOldMessages { get; }

        /// <summary>
        /// Current state of Reminder is stored in the most recent snapshot, so older SnapshotStore entries can be removed from the database.
        /// Before enabling snapshot cleanup make sure the SnapshotStore table doesn't contain large number of entries for the given PersistenceId.
        /// Deletion of large snapshots may take long time, so it may lead to timeout exception when deleting multiple snapshots. 
        /// Default: false. (Default is set to false to match Reminder's behavior when this settings was not configurable)
        /// </summary>
        public bool CleanupOldSnapshots { get; }

        public ReminderSettings(string persistenceId, string journalPluginId, string snapshotPluginId, 
            TimeSpan tickInterval, int snapshotInterval, bool cleanupOldMessages, bool cleanupOldSnapshots)
        {
            PersistenceId = persistenceId ?? throw new ArgumentNullException(nameof(persistenceId));
            JournalPluginId = journalPluginId ?? throw new ArgumentNullException(nameof(journalPluginId));
            SnapshotPluginId = snapshotPluginId ?? throw new ArgumentNullException(nameof(snapshotPluginId));
            TickInterval = tickInterval;
            SnapshotInterval = snapshotInterval;
            CleanupOldMessages = cleanupOldMessages;
            CleanupOldSnapshots = cleanupOldSnapshots;
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

        private ReminderSettings Copy(string persistenceId = null, string journalPluginId = null, string snapshotPluginId = null, 
            TimeSpan? tickInterval = null, int? snapshotInterval = null, bool? deleteUnusedJournalEntries = null, bool? deleteUnusedSnapshotEntries = null) =>
            new ReminderSettings(
                persistenceId: persistenceId ?? PersistenceId,
                journalPluginId: journalPluginId ?? JournalPluginId,
                snapshotPluginId: snapshotPluginId ?? SnapshotPluginId,
                tickInterval: tickInterval ?? TickInterval,
                snapshotInterval: snapshotInterval ?? SnapshotInterval,
                cleanupOldMessages: deleteUnusedJournalEntries ?? CleanupOldMessages,
                cleanupOldSnapshots: deleteUnusedSnapshotEntries ?? CleanupOldSnapshots);
    }
}