using EuropeyaTest.Api.Bot;
using EuropeyaTest.Api.Persistence;
using EuropeyaTest.Api.Services.Bot;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Telegram.Bot;

namespace EuropeyaTest.Api
{
    public class Program
    {
        readonly static string MySpecificCorsOrigins = "_corspolicy";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            //builder.Logging.ClearProviders();


            builder.Configuration
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .AddEnvironmentVariables();

            // Add services to the container.



            var origins = builder.Configuration.GetSection("CorsOrigins:Urls").Get<string[]>();
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(name: MySpecificCorsOrigins,
                                  builder =>
                                  {
                                      builder.WithOrigins(origins);
                                      builder.AllowAnyHeader();
                                      builder.AllowAnyMethod();
                                      builder.AllowCredentials();
                                  });
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();

            builder.Services.AddSwaggerGen();

            builder.Services.AddDbContext<BotDbContext>(Options =>
            {
                Options.UseNpgsql(Environment.GetEnvironmentVariable("DB_CS"));
            });


            // There are several strategies for completing asynchronous tasks during startup.
            // Some of them could be found in this article https://andrewlock.net/running-async-tasks-on-app-startup-in-asp-net-core-part-1/
            // We are going to use IHostedService to add and later remove Webhook
            builder.Services.AddHostedService<ConfigureWebhook>();

            // Register named HttpClient to get benefits of IHttpClientFactory
            // and consume it with ITelegramBotClient typed client.
            // More read:
            //  https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-5.0#typed-clients
            //  https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests
            builder.Services.AddHttpClient("tgwebhook")
                    .AddTypedClient<ITelegramBotClient>(httpClient
                        => new TelegramBotClient(Environment.GetEnvironmentVariable("BotToken"), httpClient));

            // The Telegram.Bot library heavily depends on Newtonsoft.Json library to deserialize
            // incoming webhook updates and send serialized responses back.
            // Read more about adding Newtonsoft.Json to ASP.NET Core pipeline:
            //   https://docs.microsoft.com/en-us/aspnet/core/web-api/advanced/formatting?view=aspnetcore-6.0#add-newtonsoftjson-based-json-format-support
            builder.Services
                .AddControllers()
                .AddNewtonsoftJson();

            var botToken = Environment.GetEnvironmentVariable("BotToken");

            builder.Services.AddScoped<ITelegramService, TelegramService>();

            builder.Services.AddScoped<HandleUpdateService>();




            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseRouting();

            app.UseCors();

            //app.UseHttpsRedirection();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(name: "tgwebhook",
                         pattern: $"bot/{botToken}",
                         new { controller = "Webhook", action = "Post" });
                endpoints.MapControllers().RequireCors(MySpecificCorsOrigins);
            });

            app.Run();
        }
    }
}