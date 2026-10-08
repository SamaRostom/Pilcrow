using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pilcrow.Data;
using Pilcrow.Dtos;
using Pilcrow.Models;
using System.Text.Json;
using Npgsql;

namespace Pilcrow.Controllers;

[ApiController]
[Route("templates")]
public class TemplatesController(PilcrowDbContext db) : ControllerBase
{
    // TODO: replace with the authenticated user once auth exists.
    private static readonly Guid DevUserId = new("00000000-0000-0000-0000-000000000001");

    [HttpGet]
    public async Task<IEnumerable<TemplateSummary>> List()
    {
        return await db.Templates
            .Where(t => t.UserId == DevUserId && t.DeletedAt == null)
            .OrderByDescending(t => t.UpdatedAt)
            .Select(t => new TemplateSummary(t.Id, t.Name, t.CurrentVersion, t.UpdatedAt))
            .ToListAsync();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TemplateDetail>> Get(Guid id)
    {
        var result = await db.Templates
            .Where(t => t.Id == id && t.UserId == DevUserId && t.DeletedAt == null)
            .Join(db.TemplateVersions,
                  t => new { TemplateId = t.Id, Version = t.CurrentVersion },
                  v => new { TemplateId = v.TemplateId, Version = v.VersionNo },
                  (t, v) => new { t.Id, t.Name, t.CurrentVersion, v.Doc })
            .FirstOrDefaultAsync();

        if (result is null) return NotFound();

        return new TemplateDetail(result.Id, result.Name, result.CurrentVersion,
                                  result.Doc.RootElement);
    }

    [HttpPost]
    public async Task<ActionResult<TemplateSummary>> Create(CreateTemplateRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Name is required.");

        var template = new Template { UserId = DevUserId, Name = req.Name.Trim() };
        var json = req.Doc.GetRawText();

        db.Templates.Add(template);
        db.TemplateVersions.Add(new TemplateVersion
        {
            TemplateId  = template.Id,
            VersionNo   = 1,
            Doc         = JsonDocument.Parse(json),
            BlockCount  = CountBlocks(req.Doc),
            CreatedBy   = DevUserId
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return Conflict($"A template named '{req.Name}' already exists.");
        }

        return CreatedAtAction(nameof(Get), new { id = template.Id },
            new TemplateSummary(template.Id, template.Name, 1, template.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TemplateSummary>> Save(Guid id, SaveTemplateRequest req)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var template = await db.Templates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == DevUserId && t.DeletedAt == null);

        if (template is null) return NotFound();

        template.CurrentVersion += 1;

        db.TemplateVersions.Add(new TemplateVersion
        {
            TemplateId  = template.Id,
            VersionNo   = template.CurrentVersion,
            Doc         = JsonDocument.Parse(req.Doc.GetRawText()),
            BlockCount  = CountBlocks(req.Doc),
            CreatedBy   = DevUserId
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return new TemplateSummary(template.Id, template.Name,
                                   template.CurrentVersion, template.UpdatedAt);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var template = await db.Templates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == DevUserId && t.DeletedAt == null);

        if (template is null) return NotFound();

        template.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static int CountBlocks(JsonElement doc) =>
        doc.TryGetProperty("blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array
            ? blocks.GetArrayLength()
            : 0;
}