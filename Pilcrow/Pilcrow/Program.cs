using Microsoft.EntityFrameworkCore;
using Pilcrow.Data;
using Pilcrow.Dtos;
using Pilcrow.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddDbContext<PilcrowDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
     .UseSnakeCaseNamingConvention());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PilcrowDbContext>();
    var devId = new Guid("00000000-0000-0000-0000-000000000001");
    if (!db.Users.Any(u => u.Id == devId))
    {
        db.Users.Add(new User { Id = devId, Email = "dev@local", PasswordHash = "x" });
        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
