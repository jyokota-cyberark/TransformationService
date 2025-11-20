namespace EntityContracts;

/// <summary>
/// Standard operations performed on entities.
/// </summary>
public static class EntityOperation
{
    public const string Created = "Created";
    public const string Updated = "Updated";
    public const string Deleted = "Deleted";
}

/// <summary>
/// Common entity types in the system.
/// Can be extended as new entity management services are added.
/// </summary>
public static class EntityTypes
{
    public const string User = "User";
    public const string WebApplication = "WebApplication";
    public const string Role = "Role";
    public const string Group = "Group";
    public const string Credential = "Credential";
}
