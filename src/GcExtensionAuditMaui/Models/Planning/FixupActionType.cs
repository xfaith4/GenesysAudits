namespace GcExtensionAuditMaui.Models.Planning;

public enum FixupActionType
{
    None = 0,

    /// <summary>PATCH user profile extension to the same value (sync attempt / reassert).</summary>
    ReassertExisting = 1,

    /// <summary>PATCH user profile extension to a specific new number.</summary>
    AssignSpecific = 2,

    /// <summary>PATCH user profile extension to blank (null).</summary>
    ClearExtension = 3,
}

