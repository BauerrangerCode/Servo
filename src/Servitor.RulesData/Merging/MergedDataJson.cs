using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Servitor.RulesData.Merging;

/// <summary>
/// The one serialization shape for the merged data, shared by the file writer, the file loader and the IndexedDB
/// store, so what one side writes the others always read back.
/// </summary>
public static class MergedDataJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        // The output is JSON files and IndexedDB strings, never embedded in HTML, so apostrophes and quotes in the
        // rules text stay readable instead of being \u-escaped.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
