namespace PhotoArchive.Core.Preprocessing;

public interface IOutputPlanner
{
    OutputPlan CreatePlan(PreprocessingRun run);
}
