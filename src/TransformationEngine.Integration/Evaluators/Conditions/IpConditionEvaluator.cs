using System.Net;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Evaluators.Conditions;

/// <summary>
/// Evaluates IP-based conditions (CIDR matching, geo, VPN detection)
/// </summary>
public class IpConditionEvaluator : ConditionEvaluatorBase
{
    private readonly ILogger<IpConditionEvaluator> _logger;
    private readonly IGeoLocationService? _geoService;

    public override string ConditionType => "IpBased";

    public IpConditionEvaluator(
        ILogger<IpConditionEvaluator> logger,
        IGeoLocationService? geoService = null)
    {
        _logger = logger;
        _geoService = geoService;
    }

    public override async Task<ConditionEvaluationResult> EvaluateAsync(
        ConditionConfig condition,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var config = condition.IpBased;
        if (config == null)
        {
            return Failed("No IP-based configuration provided");
        }

        var ipAddress = context.IpAddress;
        if (string.IsNullOrEmpty(ipAddress))
        {
            // No IP address provided - decide based on configuration
            if (config.AllowedCidrs.Count > 0 || config.RequireGeo)
            {
                return Failed("IP address required but not provided");
            }
            return Passed("No IP restrictions configured");
        }

        if (!IPAddress.TryParse(ipAddress, out var ip))
        {
            return Failed($"Invalid IP address format: {ipAddress}");
        }

        // Check blocked CIDRs first
        if (config.BlockedCidrs.Count > 0)
        {
            foreach (var cidr in config.BlockedCidrs)
            {
                if (IsInCidr(ip, cidr))
                {
                    return Failed($"IP {ipAddress} is in blocked range {cidr}",
                        new Dictionary<string, object>
                        {
                            ["ipAddress"] = ipAddress,
                            ["blockedCidr"] = cidr
                        });
                }
            }
        }

        // Check allowed CIDRs
        if (config.AllowedCidrs.Count > 0)
        {
            var isAllowed = false;
            string? matchedCidr = null;

            foreach (var cidr in config.AllowedCidrs)
            {
                if (IsInCidr(ip, cidr))
                {
                    isAllowed = true;
                    matchedCidr = cidr;
                    break;
                }
            }

            if (!isAllowed)
            {
                return Failed($"IP {ipAddress} not in allowed ranges",
                    new Dictionary<string, object>
                    {
                        ["ipAddress"] = ipAddress,
                        ["allowedCidrs"] = config.AllowedCidrs
                    });
            }

            _logger.LogDebug("IP {IP} matched allowed CIDR {CIDR}", ipAddress, matchedCidr);
        }

        // Check geo location
        if (config.AllowedCountries.Count > 0 || config.RequireGeo)
        {
            var geoResult = await CheckGeoLocationAsync(ip, config, cancellationToken);
            if (!geoResult.Passed)
            {
                return geoResult;
            }
        }

        // Check VPN (if service available)
        if (config.BlockVpn && _geoService != null)
        {
            var vpnResult = await CheckVpnAsync(ip, cancellationToken);
            if (!vpnResult.Passed)
            {
                return vpnResult;
            }
        }

        return Passed($"IP {ipAddress} passed all checks",
            new Dictionary<string, object>
            {
                ["ipAddress"] = ipAddress
            });
    }

    private bool IsInCidr(IPAddress ip, string cidr)
    {
        try
        {
            var parts = cidr.Split('/');
            if (parts.Length != 2)
            {
                // Treat as single IP
                return IPAddress.TryParse(cidr, out var singleIp) && ip.Equals(singleIp);
            }

            var networkAddress = IPAddress.Parse(parts[0]);
            var prefixLength = int.Parse(parts[1]);

            // Ensure same address family
            if (ip.AddressFamily != networkAddress.AddressFamily)
            {
                return false;
            }

            var ipBytes = ip.GetAddressBytes();
            var networkBytes = networkAddress.GetAddressBytes();

            // Calculate network mask
            var bytesToCheck = prefixLength / 8;
            var bitsInLastByte = prefixLength % 8;

            for (int i = 0; i < bytesToCheck; i++)
            {
                if (ipBytes[i] != networkBytes[i])
                    return false;
            }

            if (bitsInLastByte > 0 && bytesToCheck < ipBytes.Length)
            {
                var mask = (byte)(0xFF << (8 - bitsInLastByte));
                if ((ipBytes[bytesToCheck] & mask) != (networkBytes[bytesToCheck] & mask))
                    return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking CIDR {CIDR} for IP {IP}", cidr, ip);
            return false;
        }
    }

    private async Task<ConditionEvaluationResult> CheckGeoLocationAsync(
        IPAddress ip,
        IpConditionConfig config,
        CancellationToken cancellationToken)
    {
        if (_geoService == null)
        {
            _logger.LogWarning("Geo location service not configured");
            return config.RequireGeo
                ? Failed("Geo location service not available")
                : Passed("Geo location check skipped (service not available)");
        }

        var geoInfo = await _geoService.GetLocationAsync(ip.ToString(), cancellationToken);
        if (geoInfo == null)
        {
            return Failed($"Unable to determine geo location for IP {ip}");
        }

        if (config.AllowedCountries.Count > 0)
        {
            if (!config.AllowedCountries.Contains(geoInfo.CountryCode, StringComparer.OrdinalIgnoreCase))
            {
                return Failed($"Country {geoInfo.CountryCode} not in allowed list",
                    new Dictionary<string, object>
                    {
                        ["countryCode"] = geoInfo.CountryCode,
                        ["countryName"] = geoInfo.CountryName ?? "",
                        ["allowedCountries"] = config.AllowedCountries
                    });
            }
        }

        return Passed($"Geo location check passed ({geoInfo.CountryCode})",
            new Dictionary<string, object>
            {
                ["countryCode"] = geoInfo.CountryCode,
                ["countryName"] = geoInfo.CountryName ?? "",
                ["city"] = geoInfo.City ?? ""
            });
    }

    private async Task<ConditionEvaluationResult> CheckVpnAsync(
        IPAddress ip,
        CancellationToken cancellationToken)
    {
        if (_geoService == null)
        {
            return Passed("VPN check skipped (service not available)");
        }

        var isVpn = await _geoService.IsVpnAsync(ip.ToString(), cancellationToken);
        if (isVpn)
        {
            return Failed($"VPN/proxy connection detected for IP {ip}",
                new Dictionary<string, object>
                {
                    ["ipAddress"] = ip.ToString(),
                    ["isVpn"] = true
                });
        }

        return Passed("Not a VPN/proxy connection");
    }

    public override IEnumerable<string> Validate(ConditionConfig condition)
    {
        var errors = new List<string>();
        var config = condition.IpBased;

        if (config == null)
        {
            errors.Add("IpBased configuration is required");
            return errors;
        }

        foreach (var cidr in config.AllowedCidrs)
        {
            if (!IsValidCidr(cidr))
            {
                errors.Add($"Invalid CIDR format: {cidr}");
            }
        }

        foreach (var cidr in config.BlockedCidrs)
        {
            if (!IsValidCidr(cidr))
            {
                errors.Add($"Invalid blocked CIDR format: {cidr}");
            }
        }

        foreach (var country in config.AllowedCountries)
        {
            if (country.Length != 2)
            {
                errors.Add($"Country code must be ISO 3166-1 alpha-2 (2 characters): {country}");
            }
        }

        return errors;
    }

    private static bool IsValidCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (!IPAddress.TryParse(parts[0], out _))
            return false;

        int prefix = 0;
        if (parts.Length == 2 && !int.TryParse(parts[1], out prefix))
            return false;

        if (parts.Length == 2)
        {
            var maxPrefix = parts[0].Contains(':') ? 128 : 32; // IPv6 or IPv4
            if (prefix < 0 || prefix > maxPrefix)
                return false;
        }

        return true;
    }
}

/// <summary>
/// Interface for geo location service
/// </summary>
public interface IGeoLocationService
{
    /// <summary>
    /// Get location information for an IP address
    /// </summary>
    Task<GeoLocationInfo?> GetLocationAsync(string ipAddress, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if IP is a VPN/proxy
    /// </summary>
    Task<bool> IsVpnAsync(string ipAddress, CancellationToken cancellationToken = default);
}

/// <summary>
/// Geo location information
/// </summary>
public class GeoLocationInfo
{
    public string CountryCode { get; set; } = string.Empty;
    public string? CountryName { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Isp { get; set; }
    public bool IsVpn { get; set; }
    public bool IsProxy { get; set; }
}
