namespace Vernuko.Core;

/// <summary>
/// Identifier generation. Version 7 GUIDs are time ordered (index friendly) and globally unique,
/// which keeps a future multi-branch / sync model free of ID collisions.
/// </summary>
public static class Ids
{
    public static Guid New() => Guid.CreateVersion7();
}
