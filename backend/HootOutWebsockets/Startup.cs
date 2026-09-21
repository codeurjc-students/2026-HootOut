using Autofac;
using HootOut.Infraestructure.DI;
using Microsoft.AspNetCore.HttpOverrides;

namespace HootOut.HootOutWebsockets
{
    public class Startup
    {
        public static IRegistrationManager RegistrationManager { get; set; } = new RegistrationManager();
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
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            var websocketsOptions = new WebSocketOptions
            {
                KeepAliveTimeout = TimeSpan.FromSeconds(15)
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