using System.Text.Json;

namespace WindowsRawInputProbe;

public enum ActionKind { None, Copy, Paste, Left, Right, Up, Down, Enter, CtrlC, CtrlV }

public sealed record KeyMapping(ActionKind Single = ActionKind.None, ActionKind Double = ActionKind.None, ActionKind Long = ActionKind.None);

public sealed class MappingConfig
{
    readonly Dictionary<string, KeyMapping> mappings;
    MappingConfig(Dictionary<string, KeyMapping> mappings) => this.mappings = mappings;

    public static MappingConfig Default() => Parse("{\"keys\":{\"VK_BROWSER_HOME\":{\"single\":\"copy\",\"double\":\"paste\"}}}");

    public static MappingConfig Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new Dictionary<string, KeyMapping>(StringComparer.OrdinalIgnoreCase);
        if (!doc.RootElement.TryGetProperty("keys", out var keys)) return new(result);
        foreach (var key in keys.EnumerateObject())
        {
            ActionKind Read(string name) => key.Value.TryGetProperty(name, out var value) && Enum.TryParse<ActionKind>(value.GetString(), true, out var action) ? action : ActionKind.None;
            result[key.Name] = new(Read("single"), Read("double"), Read("long"));
        }
        return new(result);
    }

    public KeyMapping Get(string key) => mappings.TryGetValue(key, out var mapping) ? mapping : new();
}
