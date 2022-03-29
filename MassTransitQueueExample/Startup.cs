using System;
using System.Text.Json.Serialization;
using MassTransit;
using MassTransitQueueExample.Configurations;
using MassTransitQueueExample.Consumers;
using MassTransitQueueExample.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MassTransitQueueExample
{
    /// <summary>
    /// Startup
    /// </summary>
    public class Startup
    {
        /// <inheritdoc cref="Startup"/>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// Configuration
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// This method gets called by the runtime. Use this method to add services to the container.
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(o => o.ForwardedHeaders = ForwardedHeaders.All);

            services.AddCors(options =>
            {
                options.AddPolicy("allow-all", builder =>
                    builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader());
            });

            services
                .AddControllers()
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
                });

            // MassTransit
            services.AddMassTransit(o =>
            {
                o.AddConsumer<TestModelConsumer>();

                o.UsingAzureServiceBus((context, cfg) =>
                {
                    var connectionStr = Configuration.GetValue<string>("AzureServiceBus:Connection");
                    var queueName = Configuration.GetValue<string>("AzureServiceBus:QueueName");

                    cfg.Host(connectionStr);
                    
                    EndpointConvention.Map<TestModel>(new Uri($"queue:{queueName}"));
                    
                    cfg.UseServiceBusMessageScheduler();


                    cfg.ReceiveEndpoint(queueName, e =>
                    {
                        e.MaxConcurrentCalls = 1;
                        e.ConfigureConsumer<TestModelConsumer>(context);
                    });


                });
            });

            services.RegisterSwagger(Configuration);
        }

        /// <summary>
        /// This method gets called by the runtime. Use this method to configure the HTTP request pipeline. 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();

            app.UseCors("allow-all");

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                // redirect to swagger
                endpoints.MapGet("", async context =>
                {
                    context.Response.Redirect("/swagger/index.html", false);
                    await context.Response.WriteAsync("");
                });

                endpoints.MapDefaultControllerRoute();
            });

            app.UseRegisteredSwagger(Configuration);
        }
    }
}
