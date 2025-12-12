using System.Text.Json.Serialization;

namespace TransformationEngine.Integration.Models;

/// <summary>
/// Configuration for Permission rule type
/// </summary>
public class PermissionRuleConfig
{
    /// <summary>
    /// Output field name for computed permissions
    /// </summary>
    public string OutputField { get; set; } = "EffectivePermissions";

    /// <summary>
    /// Cedar-like policy DSL expression
    /// </summary>
    public string? PolicyDsl { get; set; }

    /// <summary>
    /// Evaluation strategy
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PermissionEvaluationStrategy EvaluationStrategy { get; set; } = PermissionEvaluationStrategy.PreComputeOnChange;

    /// <summary>
    /// How to combine multiple policy decisions
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PermissionCombinationMode CombinationMode { get; set; } = PermissionCombinationMode.DenyOverrides;

    /// <summary>
    /// Conditions to evaluate (temporal, IP, quota, attribute)
    /// </summary>
    public List<ConditionConfig> Conditions { get; set; } = new();

    /// <summary>
    /// Rule version for tracking changes
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Previous version ID for rollback support
    /// </summary>
    public int? PreviousVersionId { get; set; }

    /// <summary>
    /// Whether this rule is in simulation mode (A/B testing)
    /// </summary>
    public bool SimulationMode { get; set; }

    /// <summary>
    /// Sampling rate for simulation (0.0 to 1.0)
    /// </summary>
    public decimal SimulationSamplingRate { get; set; } = 0.1m;

    /// <summary>
    /// Actions this permission applies to
    /// </summary>
    public List<string> Actions { get; set; } = new() { "read", "write", "delete" };

    /// <summary>
    /// Resource types this permission applies to
    /// </summary>
    public List<string> ResourceTypes { get; set; } = new();
}

/// <summary>
/// Evaluation strategy for permission rules
/// </summary>
public enum PermissionEvaluationStrategy
{
    /// <summary>
    /// Pre-compute on entity change
    /// </summary>
    PreComputeOnChange,

    /// <summary>
    /// Lazy evaluation with caching
    /// </summary>
    LazyOnDemand,

    /// <summary>
    /// Always evaluate in real-time
    /// </summary>
    AlwaysEvaluate
}

/// <summary>
/// How to combine multiple policy decisions
/// </summary>
public enum PermissionCombinationMode
{
    /// <summary>
    /// Any explicit deny overrides all permits
    /// </summary>
    DenyOverrides,

    /// <summary>
    /// First applicable policy wins (by priority)
    /// </summary>
    FirstApplicable,

    /// <summary>
    /// Error if multiple policies match
    /// </summary>
    OnlyOneApplicable,

    /// <summary>
    /// Any permit wins unless explicit deny
    /// </summary>
    PermitOverrides
}

/// <summary>
/// Base condition configuration
/// </summary>
public class ConditionConfig
{
    /// <summary>
    /// Condition type (Temporal, IpBased, Quota, Attribute)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Temporal condition settings
    /// </summary>
    public TemporalConditionConfig? Temporal { get; set; }

    /// <summary>
    /// Business hours settings (shorthand for temporal)
    /// </summary>
    public BusinessHoursConfig? BusinessHours { get; set; }

    /// <summary>
    /// IP-based condition settings
    /// </summary>
    public IpConditionConfig? IpBased { get; set; }

    /// <summary>
    /// Quota condition settings
    /// </summary>
    public QuotaConditionConfig? Quota { get; set; }

    /// <summary>
    /// Attribute-based condition expression
    /// </summary>
    public string? Expression { get; set; }
}

/// <summary>
/// Temporal condition configuration
/// </summary>
public class TemporalConditionConfig
{
    /// <summary>
    /// Start time (24-hour format)
    /// </summary>
    public int StartHour { get; set; } = 0;

    /// <summary>
    /// End time (24-hour format)
    /// </summary>
    public int EndHour { get; set; } = 24;

    /// <summary>
    /// Timezone (IANA format)
    /// </summary>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Days of week (0=Sunday, 6=Saturday)
    /// </summary>
    public List<int> DaysOfWeek { get; set; } = new() { 1, 2, 3, 4, 5 }; // Mon-Fri

    /// <summary>
    /// Start date (inclusive)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date (inclusive)
    /// </summary>
    public DateTime? EndDate { get; set; }
}

/// <summary>
/// Business hours shorthand configuration
/// </summary>
public class BusinessHoursConfig
{
    public int Start { get; set; } = 9;
    public int End { get; set; } = 17;
    public string Timezone { get; set; } = "America/Los_Angeles";
}

/// <summary>
/// IP-based condition configuration
/// </summary>
public class IpConditionConfig
{
    /// <summary>
    /// Allowed CIDR ranges
    /// </summary>
    public List<string> AllowedCidrs { get; set; } = new();

    /// <summary>
    /// Blocked CIDR ranges
    /// </summary>
    public List<string> BlockedCidrs { get; set; } = new();

    /// <summary>
    /// Allowed countries (ISO 3166-1 alpha-2)
    /// </summary>
    public List<string> AllowedCountries { get; set; } = new();

    /// <summary>
    /// Block VPN connections
    /// </summary>
    public bool BlockVpn { get; set; }

    /// <summary>
    /// Require specific geo locations
    /// </summary>
    public bool RequireGeo { get; set; }
}

/// <summary>
/// Quota condition configuration
/// </summary>
public class QuotaConditionConfig
{
    /// <summary>
    /// Metric to track (e.g., "api_calls", "storage_gb")
    /// </summary>
    public string Metric { get; set; } = string.Empty;

    /// <summary>
    /// Maximum value per window
    /// </summary>
    public long MaxPerWindow { get; set; }

    /// <summary>
    /// Window duration in minutes
    /// </summary>
    public int WindowMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to track concurrent usage
    /// </summary>
    public bool TrackConcurrent { get; set; }

    /// <summary>
    /// Maximum concurrent usage
    /// </summary>
    public int MaxConcurrent { get; set; }
}
