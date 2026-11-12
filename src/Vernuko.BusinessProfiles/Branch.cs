using Vernuko.Core;

namespace Vernuko.BusinessProfiles;

/// <summary>
/// A business location. Phase 1 always has exactly one (the default branch), but branch-scoped tables carry
/// BranchId from day one so a multi-branch model (Model C) does not require re-keying data.
/// </summary>
public sealed class Branch : Entity
{
    public static readonly Guid DefaultId = new("00000000-0000-7000-8000-000000000001");

    private Branch()
    {
    }

    public Branch(Guid id, string name)
        : base(id)
    {
        Name = Guard.NotBlank(name).Trim();
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void Rename(string name) => Name = Guard.NotBlank(name).Trim();
}
