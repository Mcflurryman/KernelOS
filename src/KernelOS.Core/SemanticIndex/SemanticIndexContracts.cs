using KernelOS.Core.VectorIndex;

namespace KernelOS.Core.SemanticIndex;

public enum SemanticIndexStatus { NeedsRebuild, Building, Maintaining, Ready, Dirty }

public sealed record SemanticIndexSnapshot(SemanticIndexStatus Status, long CurrentGeneration, long AppliedGeneration, VectorFamilyKey? ReadyFamily);
public sealed record SemanticRebuildContext(long StartGeneration, SemanticIndexStatus PreviousStatus);

public interface ISemanticIndexCoordinator
{
    SemanticIndexSnapshot GetSnapshot();
    long RegisterMutation();
    void MarkDirty();
    SemanticRebuildContext BeginRebuild();
    void CompleteRebuild(SemanticRebuildContext context, VectorFamilyKey family);
    void AbortRebuild(SemanticRebuildContext context);
    bool CompleteIncremental(long generation, VectorFamilyKey family);
}
