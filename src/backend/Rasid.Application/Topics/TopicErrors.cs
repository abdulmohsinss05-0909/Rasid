using Rasid.Application.Common.Results;

namespace Rasid.Application.Topics;

internal static class TopicErrors
{
    public static Error NotFound(Guid id) => Error.NotFound("topic_not_found", $"Topic '{id}' was not found.");

    public static Error DuplicateName(string name) =>
        Error.Conflict("topic_name_taken", $"A topic named '{name}' already exists.");

    public static Error ConcurrencyConflict() =>
        Error.Conflict("topic_modified", "The topic was changed by someone else. Reload and try again.");

    public static Error Paused() => Error.Conflict("topic_paused", "The topic is paused. Resume it before requesting a refresh.");

    public static Error RefreshCooldown(int seconds) =>
        Error.Conflict("refresh_too_soon", $"A refresh was requested recently. Try again in about {seconds} seconds.");
}
