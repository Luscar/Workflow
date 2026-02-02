using SimpleBPM;
using SimpleBPM.Localisation;
using SimpleBPM.Monitor.Services;
using SimpleBPM.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add API services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// Add CORS for Angular dev server
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// --- SimpleBPM Registration ---
// Client applications should register:
// 1. IDbConnection (for Oracle)
// 2. ICommandExecutor (required)
// 3. IGestionTache (optional)
// 4. ProcessDefinition instances
//
// Example:
//   services.AddSimpleBPM(tablePrefix: "BPM");
//   services.AddSingleton<ProcessDefinition>(myDefinition);
//   services.AddSingleton<ICommandExecutor, MyCommandExecutor>();
//
// For development/demo, register in-memory implementations:
var tablePrefix = builder.Configuration.GetValue<string>("SimpleBPM:TablePrefix") ?? "BPM";

// Register the monitor service
builder.Services.AddScoped<IMonitorService, MonitorService>();

// Register FlowEngine explicitly for admin operations
builder.Services.AddScoped<FlowEngine>(sp =>
    new FlowEngine(
        sp.GetServices<ProcessDefinition>(),
        sp.GetRequiredService<IProcessRepository>(),
        sp.GetServices<SimpleBPM.Handlers.INodeHandler>()));

var app = builder.Build();

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseCors("AllowAngularDev");

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

// Fallback to index.html for Angular routing
app.MapFallbackToFile("index.html");

app.Run();
