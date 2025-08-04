using MapTileManager.Components;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpClient();
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri("https://localhost:7059") // ← HTTPS'e dikkat!
});



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles(); // wwwroot

// 🔥 🔥 BURAYA DİKKAT — Belgeler klasörünü kullanıyoruz:
var tileFolder = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
    "MapTileDownloads"
);

// Klasör yoksa oluştur:
if (!Directory.Exists(tileFolder))
{
    Directory.CreateDirectory(tileFolder);
}

// Bu klasördeki her şeyi /tiles altında sun:
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(tileFolder),
    RequestPath = "/tiles"
});


app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
