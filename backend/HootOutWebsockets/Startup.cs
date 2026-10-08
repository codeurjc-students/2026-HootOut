using Autofac;
using HootOut.Infraestructure.DI;
using Microsoft.AspNetCore.HttpOverrides;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace HootOut.HootOutWebsockets
{
    public class Startup
    {
        public IRegistrationManager RegistrationManager { get; set; } = new RegistrationManager();
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            JsonConvert.DefaultSettings = () => new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Ignore
            };

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            if (!env.IsDevelopment())
                app.UseHsts();

            // Backstop: if someone ever re-adds an HTTP listener, refuse instead of serving.
            // No redirect, because credentials sent over HTTP are already exposed.
            app.Use(async (context, next) =>
            {
                if (!context.Request.IsHttps)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    return;
                }
                await next();
            });

            var websocketsOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(30),
                KeepAliveTimeout = TimeSpan.FromSeconds(15),
            };

            app.UseWebSockets(websocketsOptions);

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        // ConfigureContainer is where you can register things directly
        // with Autofac. This runs after ConfigureServices so the things
        // here will override registrations made in ConfigureServices.
        // Don't build the container; that gets done for you by the factory.
        public void ConfigureContainer(ContainerBuilder builder)
        {
            //// Register your own things directly with Autofac here. Don't
            //// call builder.Populate(), that happens in AutofacServiceProviderFactory
            //// for you.

            RegistrationManager.RegisterAllAssemblies(builder);
        }
    }
}