namespace Rasid.Application.Common.Abstractions;

/// <summary>Port for queuing background work. The API enqueues; only the Worker executes.</summary>
public interface IJobScheduler
{
    /// <summary>Queues an immediate discovery run for a topic.</summary>
    string EnqueueTopicDiscovery(Guid topicId);
}
