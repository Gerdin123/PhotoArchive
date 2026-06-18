namespace PhotoArchive.Core.Preprocessing;

public interface IDateInferenceService
{
    DateInferenceResult Infer(DateInferenceEvidence evidence);
}
