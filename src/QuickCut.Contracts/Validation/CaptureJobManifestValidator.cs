using QuickCut.Contracts.Jobs;

namespace QuickCut.Contracts.Validation;

public static class CaptureJobManifestValidator
{
    public static ContractValidationResult Validate(CaptureJobManifest manifest)
    {
        List<string> errors = [];

        Require(CaptureJobManifest.CurrentSchemaVersion.Equals(manifest.SchemaVersion, StringComparison.Ordinal), "schema_version must be 1.0");
        Require(!string.IsNullOrWhiteSpace(manifest.JobId), "job_id is required");
        Require(!string.IsNullOrWhiteSpace(manifest.Source), "source is required");
        Require(!string.IsNullOrWhiteSpace(manifest.ImagePath), "image_path is required");
        Require(!string.IsNullOrWhiteSpace(manifest.ArtifactDir), "artifact_dir is required");
        Require(manifest.CreatedAt != default, "created_at is required");
        Require(manifest.Bounds.Width > 0, "bounds.width must be positive");
        Require(manifest.Bounds.Height > 0, "bounds.height must be positive");
        Require(manifest.Dpi.ScaleX > 0, "dpi.scale_x must be positive");
        Require(manifest.Dpi.ScaleY > 0, "dpi.scale_y must be positive");
        Require(!string.IsNullOrWhiteSpace(manifest.Monitor.Id), "monitor.id is required");
        Require(!string.IsNullOrWhiteSpace(manifest.Monitor.DeviceName), "monitor.device_name is required");

        return errors.Count == 0
            ? ContractValidationResult.Valid()
            : ContractValidationResult.Invalid(errors);

        void Require(bool condition, string message)
        {
            if (!condition)
            {
                errors.Add(message);
            }
        }
    }
}
