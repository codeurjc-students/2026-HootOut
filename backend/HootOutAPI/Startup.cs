using Autofac;
using HootOut.Authentication.Jwt;
using HootOut.Authentication.Services;
using HootOut.Contracts.Authentication.Services;
using HootOut.HootOutAPI.Schedulers;
using HootOut.Infraestructure.DI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

namespace HootOut.HootOutAPI
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
            var jwt = Configuration.GetSection("Jwt").Get<JwtSettings>() ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");
            var rsaKeyProvider = new DevelopmentRsaKeyProvider(jwt.PrivateKeysPath);

            services.Configure<JwtSettings>(Configuration.GetSection("Jwt"));
            services.AddSingleton<IRsaKeyProvider>(rsaKeyProvider);

            services.AddHostedService<RefreshTokenCleanupService>();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).
                AddJwtBearer(options =>
                {
                    // Keep claim names as issued (sub, name, role) instead of the legacy WS-Fed mapping.
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,

                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,

                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = rsaKeyProvider.ValidationKey,   // public key only
                        RequireSignedTokens = true,
                        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],

                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),

                        NameClaimType = "name",
                        RoleClaimType = "role"
                    };
                });

            services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());

            var allowedOrigins = Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
            services.AddCors(options => options.AddDefaultPolicy(policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins)
                          .AllowCredentials()
                          .WithMethods("GET", "POST")
                          .WithHeaders("Authorization", "Content-Type", "X-Token-Delivery");
            }));

            services.AddControllers();
            services.AddOpenApi();
            services.AddSwaggerGen();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
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

            app.UseRouting();

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                if (env.IsDevelopment())
                {
                    endpoints.MapOpenApi();
                    endpoints.MapSwagger();
                    endpoints.MapSwaggerUI();
                }
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

            /// Check for OpenAPI build time generation
            if (Environment.GetEnvironmentVariable("GENERATING_OPENAPI_DOC") != "true")
            {
                RegistrationManager.RegisterAllAssemblies(builder);
            }
        }
    }
}
