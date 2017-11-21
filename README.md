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

	// setup a message to be send to a recipient in the future
	var task = new Reminder.Schedule(Guid.NewGuid().ToString(), recipient.Path, "message", DateTime.UtcNow.AddDays(1));
	reminder.Tell(task);
	
	// get scheduled entries
	var state = await reminder.Ask<Reminder.State>(Reminder.GetState.Instance);
}
```

You can also define a reply message, that will be send back once a scheduled task has been persisted.

```csharp
var task = new Reminder.Schedule(Guid.NewGuid().ToString(), recipient.Path, "message", DateTime.UtcNow.AddDays(1), ack: "reply");
var ack = await reminder.Ask<string>(task); // ack should be "reply"
```

### Using reminder in cluster

Since a reminder is a persistent actor, it's crucial, that only one instance of it may be active in the entire cluster at the same time. In order to avoid risk of having multiple instances or rellying on a single preconfigured machine, you may combine reminder together with Akka.NET [cluster singleton](http://getakka.net/articles/clustering/cluster-singleton.html) from [Akka.Cluster.Tools](https://www.nuget.org/packages/Akka.Cluster.Tools/) package.

```csharp
system.ActorOf(ClusterSingletonManager.Props(
    singletonProps: Reminder.Props(),
    terminationMessage: PoisonPill.Instance,
    settings: ClusterSingletonManagerSettings.Create(system).WithRole("reminder")), // use role to limit reminder actor occurrence
    name: "consumer");
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