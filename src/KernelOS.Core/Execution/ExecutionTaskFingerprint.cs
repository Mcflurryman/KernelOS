using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KernelOS.Core.Planning;

namespace KernelOS.Core.Execution;

public static class ExecutionTaskFingerprint
{
    public static string Create(PlanTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("toolName", task.ToolName);
            writer.WritePropertyName("arguments");
            writer.WriteStartObject();
            foreach (var argument in task.Arguments.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(argument.Key);
                WriteCanonicalJson(writer, argument.Value);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteCanonicalJson(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonicalJson(writer, property.Value);
            }

            writer.WriteEndObject();
            return;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in value.EnumerateArray())
            {
                WriteCanonicalJson(writer, item);
            }

            writer.WriteEndArray();
            return;
        }

        value.WriteTo(writer);
    }
}
