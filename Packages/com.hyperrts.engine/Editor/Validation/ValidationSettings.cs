using HyperRTS.Editor.Common;
using UnityEditor;

namespace HyperRTS.Editor.Validation;

/// <summary>The Validate page's remembered scope and severity chips.</summary>
[FilePath("UserSettings/HyperRTSValidation.asset", FilePathAttribute.Location.ProjectFolder)]
internal sealed class ValidationSettings : UserSettings<ValidationSettings>
{
    public bool selectionOnly;

    /// <summary>Whether errors are listed.</summary>
    public bool errors = true;

    /// <summary>Whether warnings are listed.</summary>
    public bool warnings = true;
}
