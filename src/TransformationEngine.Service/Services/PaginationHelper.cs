namespace TransformationEngine.Service.Services;

/// <summary>
/// Helper class for pagination configuration and validation.
/// Ensures consistent pagination settings across the service.
/// </summary>
public static class PaginationHelper
{
    /// <summary>
    /// Default page size (15 rows)
    /// </summary>
    public const int DefaultPageSize = 15;

    /// <summary>
    /// Allowed page size options
    /// </summary>
    public static readonly int[] AllowedPageSizes = { 10, 15, 25, 50, 100 };

    /// <summary>
    /// Configuration key for default page size in database
    /// </summary>
    public const string DefaultPageSizeConfigKey = "Pagination.DefaultPageSize";

    /// <summary>
    /// Configuration key for allowed page sizes in database
    /// </summary>
    public const string AllowedPageSizesConfigKey = "Pagination.AllowedPageSizes";

    /// <summary>
    /// Validates and normalizes the requested page size.
    /// Throws exception if page size is invalid.
    /// </summary>
    /// <param name="requestedPageSize">The requested page size (0 means use default)</param>
    /// <param name="defaultPageSize">The default page size to use if not specified</param>
    /// <returns>A valid page size from AllowedPageSizes</returns>
    /// <exception cref="ArgumentException">Thrown if page size is invalid</exception>
    public static int ValidatePageSize(int requestedPageSize, int defaultPageSize = DefaultPageSize)
    {
        // If 0 or negative, use default
        if (requestedPageSize <= 0)
        {
            return defaultPageSize;
        }

        // Check if the requested size is in the allowed list
        if (!AllowedPageSizes.Contains(requestedPageSize))
        {
            throw new ArgumentException(
                $"Invalid page size: {requestedPageSize}. Allowed sizes are: {string.Join(", ", AllowedPageSizes)}",
                nameof(requestedPageSize));
        }

        return requestedPageSize;
    }

    /// <summary>
    /// Calculates the skip value for LINQ pagination
    /// </summary>
    public static int CalculateSkip(int page, int pageSize)
    {
        if (page < 1) page = 1;
        return (page - 1) * pageSize;
    }

    /// <summary>
    /// Calculates total pages
    /// </summary>
    public static int CalculateTotalPages(int totalCount, int pageSize)
    {
        return (int)Math.Ceiling(totalCount / (double)pageSize);
    }
}
