# Akka.Persistence.Reminders

An Akka.NET scheduler designed to work with long running tasks. When compared to standard `ActorSystem` scheduler, there are two major differences:

- Standard scheduler state is stored in memory. That means, it's going to be lost after `ActorSystem` or machine restart. Reminder state is backed by an Akka.Persistence eventsourcing engine, which means that it's able to survive between restarts, making it good choice for tasks designed to be fired hours, days or many weeks in the future.
- Unlike standard scheduler, reminder is not designed to work with sub-second latencies. If this is your case, don't use reminders.

### Basic example


```csharp
var config = ConfigurationFactory.Load().WithFallback(Reminder.DefaultConfig);
using (var system = ActorSystem.Create("system", config))
{
	// create a reminder
	var reminder = system.ActorOf(Reminder.Props(), "reminder");
	
	var taskId = Guid.NewGuid().ToString();

	// setup a message to be send to a recipient in the future
	var task = new Reminder.Schedule(taskId, recipient.Path, "message", DateTime.UtcNow.AddDays(1));
	// setup a message to be send in hour intervals
	var task = new Reminder.ScheduleRepeatedly(taskId, recipient.Path, "message", DateTime.UtcNow.AddDays(1), TimeSpan.FromHours(1));
	// setup a message to be send at 10:15am on the third Friday of every month
	var task = new Reminder.ScheduleCron(taskId, recipient.Path, "message", DateTime.UtcNow.AddDays(1), "15 10 ? * 6#3");
	reminder.Tell(task);
	
	// get scheduled entries
	var state = await reminder.Ask<Reminder.State>(Reminder.GetState.Instance);

	// cancel previously scheduled entity - if ack was defined it will be returned to sender after completion
	reminder.Tell(new Reminder.Cancel(taskId))
}
```

You can also define a reply message, that will be send back once a scheduled task has been persisted.

```csharp
var task = new Reminder.Schedule(Guid.NewGuid().ToString(), recipient.Path, "message", DateTime.UtcNow.AddDays(1), ack: "reply");
var ack = await reminder.Ask<string>(task); // ack should be "reply"
```

### Cron Expressions

You can setup schedule to repeat itself accordingly to following cron expressions format:

```
                                       Allowed values    Allowed special characters 

 ┌───────────── minute                0-59              * , - /                      
 │ ┌───────────── hour                0-23              * , - /                     
 │ │ ┌───────────── day of month      1-31              * , - / L W ?               
 │ │ │ ┌───────────── month           1-12 or JAN-DEC   * , - /                     
 │ │ │ │ ┌───────────── day of week   0-6  or SUN-SAT   * , - / # L ?               
 │ │ │ │ │
 * * * * *
```

Other characteristics (supported by a [Cronos](https://github.com/HangfireIO/Cronos) library used by this plugin):

- Supports non-standard characters like L, W, # and their combinations.
- Supports reversed ranges, like 23-01 (equivalent to 23,00,01) or DEC-FEB (equivalent to DEC,JAN,FEB).
- Supports time zones, and performs all the date/time conversions for you.
- Does not skip occurrences, when the clock jumps forward to Daylight saving time (known as Summer time).
- Does not skip interval-based occurrences, when the clock jumps backward from Summer time.
- Does not retry non-interval based occurrences, when the clock jumps backward from Summer time.

### Using reminder in cluster

Since a reminder is a persistent actor, it's crucial, that only one instance of it may be active in the entire cluster at the same time. In order to avoid risk of having multiple instances or rellying on a single preconfigured machine, you may combine reminder together with Akka.NET [cluster singleton](http://getakka.net/articles/clustering/cluster-singleton.html) from [Akka.Cluster.Tools](https://www.nuget.org/packages/Akka.Cluster.Tools/) package.

```csharp
system.ActorOf(ClusterSingletonManager.Props(
    singletonProps: Reminder.Props(),
    terminationMessage: PoisonPill.Instance,
	// use role to limit reminder actor placement only to some subset of nodes
    settings: ClusterSingletonManagerSettings.Create(system).WithRole("reminder")), 
    name: "reminder");
```

### Using reminders together with Akka.Cluster.Sharding

Since sharded actors don't have a single `ActorPath` as they can move between different nodes of the cluster, it's not reliable to set that actor's path as a reminder recipient. In this case a recipient should be the shard region living on a current cluster node, while the message itself should be a shard envelope having an information necessary to route the payload to target sharded actor.

```csharp
var region = ClusterSharding.Get(system).Start(
    typeName: "my-actor",
    entityProps: Props.Create<MyActor>(),
    settings: ClusterShardingSettings.Create(system),
    messageExtractor: new MessageExtractor());

var envelope = new Envelope(shardId: 1, entityId: 1, message: "hello");
var task = new Reminder.Schedule(taskId, region.Path, envelope, DateTime.UtcNow.AddDays(1));
reminder.Tell(task);
```

### Configuration

```hocon
akka.persistence.reminder {

	# Persistent identifier for event stream produced by correlated reminder.
	persistence-id = "reminder"

	# Identifier of a event journal used by correlated reminder.
	journal-plugin-id = ""

	# Identifer of a snapshot store used by correlated reminder.
	snapshot-plugin-id = ""

	# Unlike standard akka.net scheduler, reminders work in much lower frequency.
	# Reason for this is that they are designed for a long running tasks (think of
	# seconds, minutes, hours, days or weeks).
	tick-inverval = 10s

	# Reminder uses standard akka.persistence eventsourcing for maintaining scheduler
	# internal state. In order to make a stream of events shorter (and ultimately 
	# allowing for a faster recovery in case of failure), a reminder state snapshot is
	# performed after a series of consecutive events have been stored.
	snapshot-interval = 100

        # Reminder periodically saves its state in Snapshot store, so old EventJournal entries can be removed from the database.
        # Default: true. (Default is set to true to match Reminder's behavior when this settings was not configurable)
        cleanup-old-messages = true
    
        # Current state of Reminder is stored in the most recent snapshot, so older SnapshotStore entries can be removed from the database.
        # Before enabling snapshot cleanup make sure the SnapshotStore table doesn't contain large number of entries for the given PersistenceId.
        # Deletion of large snapshots may take long time, so it may lead to timeout exception when deleting multiple snapshots. 
        # Default: false. (Default is set to false to match Reminder's behavior when this settings was not configurable)
        cleanup-old-snapshots = false
}
```
