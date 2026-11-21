using System.ComponentModel.DataAnnotations;

namespace MyServerManager.Models;

public sealed record ApplicationDefinition
{
    [Required, StringLength(100)]
    public string DisplayName { get; init; } = "";

    [Required, StringLength(128)]
    public string ServiceName { get; init; } = "";

    [StringLength(400)]
    public string? DeploymentDirectory { get; init; }

    [StringLength(150)]
    public string? AssemblyName { get; init; }

    [Range(1, 65535)]
    public int? Port { get; init; }

    [StringLength(500)]
    public string? WebsiteUrl { get; init; }

    [StringLength(50)]
    public string? Environment { get; init; }

    [StringLength(400)]
    public string? Description { get; init; }

    public bool Enabled { get; init; } = true;

    [Range(0, 10000)]
    public int DisplayOrder { get; init; }
}

public sealed record ManagerPreferences
{
    [RegularExpression("^(light|dark)$"), Required]
    public string Theme { get; init; } = "light";

    [Range(5, 300)] public int ApplicationSeconds { get; init; } = 10;
    [Range(3, 60)] public int CpuSeconds { get; init; } = 5;
    [Range(5, 300)] public int MemorySeconds { get; init; } = 10;
    [Range(30, 600)] public int DiskSeconds { get; init; } = 60;
    [Range(30, 3600)] public int WebsiteSeconds { get; init; } = 60;
    [Range(15, 600)] public int SqlSeconds { get; init; } = 30;
}
