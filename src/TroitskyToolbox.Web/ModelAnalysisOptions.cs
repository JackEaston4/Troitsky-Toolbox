using System.ComponentModel.DataAnnotations;

namespace TroitskyToolbox.Web;

/// <summary>Settings for the Model Analysis endpoints, bound from the "ModelAnalysis" config section.</summary>
public sealed class ModelAnalysisOptions
{
    public const string Section = "ModelAnalysis";

    /// <summary>Largest accepted upload. POC value; the SRS does not set one yet.</summary>
    [Range(typeof(long), "1", "2147483647", ErrorMessage = "MaxUploadBytes must be between 1 and 2147483647 bytes.")]
    public long MaxUploadBytes { get; set; } = 50 * 1024 * 1024;

    /// <summary>MA_TIMEOUT from the Hazard Analysis (SR-MA-3): server processing stops after this.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "TimeoutSeconds must be greater than zero.")]
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Folder of sample STEP files offered on the page, relative to the content root.</summary>
    [Required(ErrorMessage = "SampleFolder must not be empty.")]
    public string SampleFolder { get; set; } = "../sample_step_files";
}
