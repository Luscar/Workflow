using System.Data;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Oracle.ManagedDataAccess.Client;
using SimpleBPM.Blazor.Components;
using SimpleBPM.Localisation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var oracleConnectionString = builder.Configuration.GetConnectionString("Oracle")
    ?? throw new InvalidOperationException("Missing 'ConnectionStrings:Oracle' in configuration.");
var oracleTablePrefix = builder.Configuration["SimpleBPM:TablePrefix"] ?? "BPM";

// Use Autofac as the DI container.
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    // Register Oracle connection for the monitoring repository.
    containerBuilder.Register<IDbConnection>(_ =>
    {
        var connection = new OracleConnection(oracleConnectionString);
        connection.Open();
        return connection;
    }).As<IDbConnection>().InstancePerLifetimeScope();

    // Monitoring-only registration with Oracle persistence.
    containerBuilder.RegisterModule(new ProcessMonitoringAutofacModule(m =>
        m.UseOracle(oracleTablePrefix)));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
