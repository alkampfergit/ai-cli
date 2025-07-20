namespace AiCli.Attributes;

/// <summary>
/// Attribute to mark properties that should be encrypted when serializing to settings file
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class EncryptedSettingAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the EncryptedSettingAttribute class
    /// </summary>
    public EncryptedSettingAttribute()
    {
    }
}