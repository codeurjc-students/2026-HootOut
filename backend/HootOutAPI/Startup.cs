using Autofac;
using HootOut.Authentication.Jwt;
using HootOut.Authentication.Services;
using HootOut.Contracts.Authentication.Services;
using HootOut.FileStorage.Configuration;
using HootOut.HootOutAPI.Schedulers;
using HootOut.Infraestructure.DI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using MimeDetective;
using MimeDetective.Definitions;

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
            var jwtConfig = Configuration.GetSection("Jwt");
            var jwt = jwtConfig.Get<JwtSettings>() ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");
            var rsaKeyProvider = new DevelopmentRsaKeyProvider(jwt.PrivateKeysPath);

            services.Configure<JwtSettings>(jwtConfig);

            var fileStorageConfig = Configuration.GetSection("FileStorage");
            services.Configure<FileStorageSettings>(fileStorageConfig);
            services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = fileStorageConfig?.Get<FileStorageSettings>()?.MaxFileBytes ?? 20 * 1024 * 1024; //Max 20MB
            });

            services.AddSingleton(new ContentInspectorBuilder
            {
                Definitions = new CondensedBuilder
                {
                    UsageType = MimeDetective.Definitions.Licensing.UsageType.PersonalNonCommercial
                }.Build()
            }.Build());

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
                          .WithMethods("GET", "POST", "PUT", "DELETE")
                          .WithHeaders("Authorization", "Content-Type", "X-Token-Delivery");
                policy.AllowAnyMethod();
            }));

            services.AddControllers();
            services.AddOpenApi();
            services.AddSwaggerGen();
            services.AddHealthChecks();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            var fwd = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            };

            fwd.KnownIPNetworks.Clear();
            fwd.KnownProxies.Clear();
            app.UseForwardedHeaders(fwd);

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

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseRouting(); 
            app.UseCors();  

            app.UseAuthentication();
            app.UseAuthorization(); 

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/health").AllowAnonymous();
                if (env.IsDevelopment())
                {
                    endpoints.MapOpenApi();
                    endpoints.MapSwagger();
                    endpoints.MapSwaggerUI();
                }

                // 404 for /api that doesnt exist
                endpoints.Map("/api/{**rest}", ctx =>
                {
                    ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
                }).AllowAnonymous();

                // Any other route returns the index.html
                endpoints.MapFallbackToFile("index.html").AllowAnonymous();
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
