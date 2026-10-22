namespace Rasid.Domain.Topics;

/// <summary>Drives refresh cadence and how the Quota Manager protects the API budget.</summary>
public enum TopicPriority
{
    Low = 1,
    Normal = 2,
    High = 3,
}
