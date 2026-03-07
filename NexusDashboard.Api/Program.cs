using NexusDashboard.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<LogParserService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins("https://localhost:7001", "http://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("BlazorClient");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
