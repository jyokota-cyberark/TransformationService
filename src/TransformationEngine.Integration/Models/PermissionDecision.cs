namespace TransformationEngine.Integration.Models;

/// <summary>
/// Result of permission evaluation
/// </summary>
public class PermissionDecision
{
    /// <summary>
    /// The decision (Allow, Deny, NotApplicable)
    /// </summary>
    public PermissionEffect Effect { get; set; } = PermissionEffect.NotApplicable;

    /// <summary>
    /// List of effective permissions granted
    /// </summary>
    public List<string> EffectivePermissions { get; set; } = new();

    /// <summary>
    /// IDs of policies that were applied
    /// </summary>
    public List<int> AppliedPolicyIds { get; set; } = new();

    /// <summary>
    /// Names of rules that were applied
    /// </summary>
    public List<string> AppliedRuleNames { get; set; } = new();

    /// <summary>
    /// Conditions that were evaluated
    /// </summary>
    public List<ConditionEvaluationResult> ConditionsEvaluated { get; set; } = new();

    /// <summary>
    /// Total evaluation time in milliseconds
    /// </summary>
    public long EvaluationTimeMs { get; set; }

    /// <summary>
    /// Whether this was from a cached result
    /// </summary>
    public bool FromCache { get; set; }

    /// <summary>
    /// Reason for the decision
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Create an allow decision
    /// </summary>
    public static PermissionDecision Allow(List<string> permissions, string? reason = null)
    {
        return new PermissionDecision
        {
            Effect = PermissionEffect.Allow,
            EffectivePermissions = permissions,
            Reason = reason ?? "Access granted"
        };
    }

    /// <summary>
    /// Create a deny decision
    /// </summary>
    public static PermissionDecision Deny(string reason)
    {
        return new PermissionDecision
        {
            Effect = PermissionEffect.Deny,
            EffectivePermissions = new List<string>(),
            Reason = reason
        };
    }

    /// <summary>
    /// Create a not applicable decision
    /// </summary>
    public static PermissionDecision NotApplicable(string reason)
    {
        return new PermissionDecision
        {
            Effect = PermissionEffect.NotApplicable,
            EffectivePermissions = new List<string>(),
            Reason = reason
        };
    }
}

/// <summary>
/// Permission effect
/// </summary>
public enum PermissionEffect
{
    /// <summary>
    /// Access is allowed
    /// </summary>
    Allow,

    /// <summary>
    /// Access is denied
    /// </summary>
    Deny,

    /// <summary>
    /// No applicable policy found
    /// </summary>
    NotApplicable
}

/// <summary>
/// Result of evaluating a single condition
/// </summary>
public class ConditionEvaluationResult
{
    /// <summary>
    /// Condition type that was evaluated
    /// </summary>
    public string ConditionType { get; set; } = string.Empty;

    /// <summary>
    /// Whether the condition passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Reason for the result
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Evaluation time in milliseconds
    /// </summary>
    public long EvaluationTimeMs { get; set; }

    /// <summary>
    /// Additional details
    /// </summary>
    public Dictionary<string, object>? Details { get; set; }
}

/// <summary>
/// Context for permission evaluation
/// </summary>
public class PermissionEvaluationContext
{
    /// <summary>
    /// Principal (user/role/group) being evaluated
    /// </summary>
    public PrincipalInfo Principal { get; set; } = new();

    /// <summary>
    /// Resource being accessed
    /// </summary>
    public ResourceInfo Resource { get; set; } = new();

    /// <summary>
    /// Action being performed
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Current timestamp
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Request IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Additional context attributes
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    /// <summary>
    /// Tenant ID
    /// </summary>
    public int? TenantId { get; set; }
}

/// <summary>
/// Principal (user/role/group) information
/// </summary>
public class PrincipalInfo
{
    /// <summary>
    /// Principal ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Principal type (User, Role, Group)
    /// </summary>
    public string Type { get; set; } = "User";

    /// <summary>
    /// Principal attributes
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    /// <summary>
    /// Role IDs the principal belongs to
    /// </summary>
    public List<int> RoleIds { get; set; } = new();

    /// <summary>
    /// Group IDs the principal belongs to
    /// </summary>
    public List<int> GroupIds { get; set; } = new();
}

/// <summary>
/// Resource information
/// </summary>
public class ResourceInfo
{
    /// <summary>
    /// Resource ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Resource type (Application, Database, File, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Resource attributes
    /// </summary>
    public Dictionary<string, object> Attributes { get; set; } = new();

    /// <summary>
    /// Owner principal ID
    /// </summary>
    public int? OwnerId { get; set; }

    /// <summary>
    /// Classification level
    /// </summary>
    public string? Classification { get; set; }
}

/// <summary>
/// Simulation result comparing two policy versions
/// </summary>
public class PermissionSimulationResult
{
    /// <summary>
    /// Rule ID being simulated
    /// </summary>
    public int RuleId { get; set; }

    /// <summary>
    /// Principal ID
    /// </summary>
    public int PrincipalId { get; set; }

    /// <summary>
    /// Resource ID
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Action being evaluated
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Decision from active (current) policy
    /// </summary>
    public PermissionEffect ActiveDecision { get; set; }

    /// <summary>
    /// Decision from simulation policy
    /// </summary>
    public PermissionEffect SimulationDecision { get; set; }

    /// <summary>
    /// Whether decisions match
    /// </summary>
    public bool DecisionsMatch => ActiveDecision == SimulationDecision;

    /// <summary>
    /// Active policy evaluation time
    /// </summary>
    public long ActiveEvaluationTimeMs { get; set; }

    /// <summary>
    /// Simulation policy evaluation time
    /// </summary>
    public long SimulationEvaluationTimeMs { get; set; }

    /// <summary>
    /// Latency delta (simulation - active)
    /// </summary>
    public long LatencyDeltaMs => SimulationEvaluationTimeMs - ActiveEvaluationTimeMs;

    /// <summary>
    /// Evaluation context used
    /// </summary>
    public PermissionEvaluationContext? Context { get; set; }

    /// <summary>
    /// When evaluation occurred
    /// </summary>
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
}
