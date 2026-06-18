namespace PhotoArchive.Core.Preprocessing;

public sealed record PreprocessingSettings(
    string InputRoot,
    string OutputRoot,
    bool Execute,
    bool AllowOutputInsideInput = false,
    string AppVersion = "0.1.0");
