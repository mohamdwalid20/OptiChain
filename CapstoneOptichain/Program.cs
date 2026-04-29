using CapstoneOptichain.Data;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Razor;
using System.Globalization;
using System.Text.Json;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<ProjectContext>();


builder.Services.AddHttpClient();
builder.Services.AddControllersWithViews()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();
builder.Services.AddSession();


builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");


var supportedCultures = new[] { new CultureInfo("en"), new CultureInfo("ar") };
supportedCultures[1].DateTimeFormat = supportedCultures[0].DateTimeFormat;
supportedCultures[1].NumberFormat = supportedCultures[0].NumberFormat;
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("en");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;


    options.FallBackToParentCultures = true;
    options.FallBackToParentUICultures = true;
    options.ApplyCurrentCultureToResponseHeaders = true;


    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    };
});

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
   

    app.UseHsts();
}

var isDevelopment = app.Environment.IsDevelopment();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var error = context.Features.Get<IExceptionHandlerFeature>();
        if (error != null)
        {
            var ex = error.Error;
            var payload = isDevelopment
                ? (object)new { error = "An unexpected error occurred", message = ex.Message, details = ex.StackTrace }
                : (object)new { error = "An unexpected error occurred" };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    });
});



var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures,
    FallBackToParentCultures = true,
    FallBackToParentUICultures = true,
    ApplyCurrentCultureToResponseHeaders = true
};


localizationOptions.RequestCultureProviders.Insert(0, new CustomRequestCultureProvider(async context =>
{

    var cookieCulture = context.Request.Cookies[CookieRequestCultureProvider.DefaultCookieName];


    if (string.IsNullOrEmpty(cookieCulture) || cookieCulture.Contains("en"))
    {
        return new ProviderCultureResult("en");
    }

    return new ProviderCultureResult(cookieCulture);
}));

app.UseRequestLocalization(localizationOptions);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Land}/{id?}");

app.Run();