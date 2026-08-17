using System.Text.Json.Serialization;

namespace UmbracoMediaAudit.Models;

/// <summary>
/// One execution of the media audit. Maps to spec.md's "Audit Run" entity. Held in memory for the
/// duration of a backoffice session (spec Assumptions) - re-running the audit replaces it.
/// </summary>
public sealed class AuditRun
{
    /// <summary>Timestamp the audit completed. Null while <see cref="Status"/> is Running and no prior run exists.</summary>
    public DateTime? RunAt { get; set; }

    public int TotalScanned { get; set; }

    public int UsedCount { get; set; }

    public long UsedSizeBytes { get; set; }

    public int UnusedCount { get; set; }

    public long UnusedSizeBytes { get; set; }

    /// <summary>Drives the progress indicator (FR-011, User Story 1 Acceptance Scenario 3).</summary>
    public AuditRunStatus Status { get; set; } = AuditRunStatus.Complete;

    public int? DurationMs { get; set; }

    /// <summary>
    /// Set when <see cref="Status"/> is Failed (T060 / spec.md's audit-failure edge case) - surfaced to
    /// the user instead of silently showing stale or partial results.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuditRunStatus
{
    Running,
    Complete,
    Failed,
}
