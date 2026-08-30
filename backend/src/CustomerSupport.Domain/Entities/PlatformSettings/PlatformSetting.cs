namespace CustomerSupport.Domain.Entities.PlatformSettings;

public class PlatformSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ValueType { get; set; } = "String";
    public bool IsEncrypted { get; set; }
    public bool IsPublic { get; set; }
}
