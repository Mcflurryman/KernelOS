using System.Text.Json;

namespace KernelOS.Core;

public sealed record ToolExecutionResult(
    ToolExecutionStatus Status,
    string Message,
    JsonElement? Data = null)
{
    public static ToolExecutionResult Success(string message, JsonElement? data = null) =>
        new(ToolExecutionStatus.Success, message, data);

    public static ToolExecutionResult InvalidArguments(string message) =>
        new(ToolExecutionStatus.InvalidArguments, message);

    public static ToolExecutionResult NotFound(string message) =>
        new(ToolExecutionStatus.NotFound, message);

    public static ToolExecutionResult Cancelled(string message = "The tool execution was cancelled.") =>
        new(ToolExecutionStatus.Cancelled, message);

    public static ToolExecutionResult Failure(string message = "The tool could not complete the requested operation.") =>
        new(ToolExecutionStatus.Failure, message);
}
