using System;
using System.Collections.Generic;
using System.Text;

namespace TroitskyToolbox.ModelAnalysis;

/// <summary>
/// Kinds for the STEP Error
/// </summary>
public enum StepErrorKind
{
    /// <summary>
    /// File of an unsupported extension
    /// </summary>
    UnsupportedExtension,
    /// <summary>
    /// File too large
    /// </summary>
    TooLarge,
    /// <summary>
    /// File does not begin with required header
    /// </summary>
    NotStep,
    /// <summary>
    /// File malformed (i.e. fails during parsing)
    /// </summary>
    Malformed,
    /// <summary>
    /// File contains no readable geometry (no solids or faces present)
    /// </summary>
    NoGeometry,
    /// <summary>
    /// File took too long to parse (i.e. timed out)
    /// </summary>
    Timeout,
}
