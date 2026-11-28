
using QLess.Data;
using DotNetEnv;

Env.Load();


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.AddSingleton<DbConnectionFactory>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Client", "CustomerOnly");
    options.Conventions.AuthorizeFolder("/Employee", "EmployeeOnly");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");

    options.Conventions.AllowAnonymousToFolder("/Account");
});

// Configure authentication
builder.Services
    .AddAuthentication("QLessCookie")
    .AddCookie("QLessCookie", options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.Cookie.Name = "QLess.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

// Configure authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CustomerOnly",
        policy => policy.RequireRole("CUSTOMER"));

    options.AddPolicy("EmployeeOnly",
        policy => policy.RequireRole("EMPLOYEE"));

    options.AddPolicy("AdminOnly",
        policy => policy.RequireRole("ADMIN"));
});


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
