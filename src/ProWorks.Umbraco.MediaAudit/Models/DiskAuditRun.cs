using System.Text.Json.Serialization;

namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>
/// One execution of the disk scan (spec.md "Disk Audit Run"). Mirrors <see cref="AuditRun"/>'s
/// Running/Complete/Failed lifecycle, held in memory for the duration of a backoffice session and
/// replaced by each new run.
/// </summary>
public sealed class DiskAuditRun
{
    /// <summary>Timestamp the scan completed. Null while <see cref="Status"/> is Running and no prior run exists.</summary>
    public DateTime? RunAt { get; set; }

    /// <summary>Every file the scan walked, owned by an Umbraco media item or not.</summary>
    public int TotalFilesOnDisk { get; set; }

    public int DiskOnlyCount { get; set; }

    public long DiskOnlySizeBytes { get; set; }

    public DiskAuditRunStatus Status { get; set; } = DiskAuditRunStatus.Complete;

    public int? DurationMs { get; set; }

    /// <summary>Set when <see cref="Status"/> is Failed - surfaced to the user instead of silently showing stale or partial results.</summary>
    public string? ErrorMessage { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DiskAuditRunStatus
{
    Running,
    Complete,
    Failed,
}
