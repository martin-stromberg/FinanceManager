namespace FinanceManager.Web.ViewModels.Setup;

/// <summary>
/// Implemented by view models that can trigger a file upload request towards the UI.
/// </summary>
public interface IUploadTrigger
{
    /// <summary>
    /// Triggers the upload request, instructing the UI to open a file picker.
    /// </summary>
    void TriggerUploadRequest();
}
