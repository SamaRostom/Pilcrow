using System.Text.Json;

namespace Pilcrow.Dtos;

public record TemplateSummary(Guid Id, string Name, int CurrentVersion, DateTimeOffset UpdatedAt);

public record TemplateDetail(Guid Id, string Name, int CurrentVersion, JsonElement Doc);

public record CreateTemplateRequest(string Name, JsonElement Doc);

public record SaveTemplateRequest(JsonElement Doc);