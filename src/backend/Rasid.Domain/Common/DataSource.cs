namespace Rasid.Domain.Common;

/// <summary>Where a piece of data came from. Always surfaced to users for transparency.</summary>
public enum DataSource
{
    /// <summary>Official X API v2.</summary>
    XApi = 1,

    /// <summary>Clearly labelled, fictitious demonstration data.</summary>
    Sample = 2,
}
