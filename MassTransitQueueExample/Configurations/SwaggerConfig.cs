using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace MassTransitQueueExample.Configurations
{
    internal static class SwaggerConfig
    {
        /// <summary>
        /// Настройки регистрации swagger
        /// </summary>
        public class RegisterSwaggerOptions
        {
            /// <summary>
            /// Схема авторизации
            /// </summary>
            public OpenApiSecurityScheme SecurityScheme { get; set; }

            /// <summary>
            /// Namespaces для аннотаций
            /// </summary>
            public string[] AssembliesForAnnotations { get; set; }

            /// <summary>
            /// Убрать отображение информации по ролям
            /// </summary>
            public bool DisableRolesInfo { get; set; }

            /// <summary>
            /// Убрать отображение информации по политикам
            /// </summary>
            public bool DisablePoliciesInfo { get; set; }
        }

        /// <summary>
        /// RegisterSwagger
        /// </summary>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <param name="registerOptions"></param>
        /// <returns></returns>
        public static IServiceCollection RegisterSwagger(this IServiceCollection services, IConfiguration configuration,
            Action<RegisterSwaggerOptions> registerOptions = null)
        {
            var apiDescription = configuration.GetSection("ApiInfoSettings");

            var apiVersionService = (IApiVersionDescriptionProvider)services.BuildServiceProvider().GetService(typeof(IApiVersionDescriptionProvider));

            static string GetXmlCommentPath(string assemblyName) => Path.Combine(AppContext.BaseDirectory, assemblyName + ".xml");

            var registerOptionsData = new RegisterSwaggerOptions();
            registerOptions?.Invoke(registerOptionsData);

            services.AddSwaggerGen(c =>
            {
                c.EnableAnnotations();

                if (registerOptionsData.AssembliesForAnnotations?.Any() ?? false)
                {
                    foreach (var annotationNamespace in registerOptionsData.AssembliesForAnnotations)
                    {
                        var xmlPath = GetXmlCommentPath(annotationNamespace);
                        if (File.Exists(xmlPath))
                        {
                            c.IncludeXmlComments(xmlPath, true);
                        }
                    }
                }

                // Priority for sorting
                var sorting = new[]
                    {
                        "GET",
                        "HEAD",
                        "POST",
                        "PUT",
                        "PATCH",
                        "DELETE",
                        "OPTIONS",
                        "CONNECT",
                        "TRACE",
                        "UNKNOWN",
                    }
                    .Select((value, index) => new { value, index })
                    .ToDictionary(i => i.value, i => i.index.ToString().PadLeft(2, '0'));

                // Controllers sorting
                c.TagActionsBy(apiDesc =>
                {
                    var controllerName = apiDesc.ActionDescriptor.RouteValues["controller"];
                    return new[] { controllerName };
                });

                // Methods sorting
                c.OrderActionsBy(apiDesc =>
                {
                    var method = apiDesc.HttpMethod?.ToUpper() ?? "UNKNOWN";
                    var sortingNumber = sorting.ContainsKey(method) ? sorting[method] : sorting["UNKNOWN"];

                    var path = apiDesc.RelativePath.Replace('/', '_');

                    return $"{path}_{sortingNumber}_{method}";
                });

                if (apiVersionService != null)
                {
                    // Documentation for versions
                    foreach (var description in apiVersionService.ApiVersionDescriptions)
                    {
                        c.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description, apiDescription));
                    }
                }
                else
                {
                    c.SwaggerDoc("V1", CreateInfoForApiVersion(null, apiDescription));
                }

                // Auth
                // Авторизация
                if (registerOptionsData.SecurityScheme != null)
                {
                    c.AddSecurityDefinition("Bearer", registerOptionsData.SecurityScheme);
                }

                // Auth иконка для методов
                c.OperationFilter<AddAuthorizationHeaderOperationHeader>(registerOptionsData);
            });

            return services;
        }

        /// <summary>
        /// UseRegisteredSwagger
        /// </summary>
        /// <param name="app"></param>
        /// <param name="configuration">Configuration</param>
        /// <returns></returns>
        public static IApplicationBuilder UseRegisteredSwagger(this IApplicationBuilder app, IConfiguration configuration)
        {

            var apiVersionService = (IApiVersionDescriptionProvider)app.ApplicationServices.GetService(typeof(IApiVersionDescriptionProvider));
            var apiDescription = configuration?.GetSection("ApiInfoSettings");

            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.DocumentTitle = apiDescription?.GetSection("Title")?.Value ?? "Swagger";
                if (!string.IsNullOrEmpty(apiDescription?.GetSection("StylesPath")?.Value))
                {
                    options.InjectStylesheet(apiDescription.GetSection("StylesPath").Value);
                }

                if (apiVersionService != null)
                {
                    foreach (var description in apiVersionService.ApiVersionDescriptions)
                        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                }
                else
                {
                    options.SwaggerEndpoint("/swagger/V1/swagger.json", "V1");
                }

            });

            return app;
        }


        /// <summary>
        /// CreateInfoForApiVersion
        /// </summary>
        /// <param name="description"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description, IConfiguration configuration)
        {
            var baseInfo = configuration?.GetSection("Description").Value;

            var info = new OpenApiInfo
            {
                Title = configuration?.GetSection("Title").Value,
                Version = description?.ApiVersion.ToString() ?? "1.0",
                Description = baseInfo + "<br/><br/>" + configuration?.GetSection($"{description?.ApiVersion}:Description").Value
            };

            if (description?.IsDeprecated ?? false)
            {
                info.Description += "<br/><br/><b>This API version has been deprecated.</b>";
            }

            return info;
        }

        /// <summary>
        /// Фильтр для корректного вывода методов требующих авторизации и нет
        /// </summary>
        internal class AddAuthorizationHeaderOperationHeader : IOperationFilter
        {
            private readonly RegisterSwaggerOptions _registerSwaggerOptions;

            public AddAuthorizationHeaderOperationHeader(RegisterSwaggerOptions registerSwaggerOptions)
            {
                _registerSwaggerOptions = registerSwaggerOptions;
            }

            public void Apply(OpenApiOperation operation, OperationFilterContext context)
            {
                var actionMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;
                var authorizedAttributes = actionMetadata.OfType<AuthorizeAttribute>().ToArray();
                var allowAnonymous = actionMetadata.Any(i => i is AllowAnonymousAttribute);

                if (!authorizedAttributes.Any() || allowAnonymous)
                {
                    return;
                }
                operation.Parameters ??= new List<OpenApiParameter>();

                // Roles to summary
                if (!_registerSwaggerOptions.DisableRolesInfo)
                {
                    var rolesStr = string.Join(", ",
                        authorizedAttributes
                            .Where(i => !string.IsNullOrEmpty(i.Roles))
                            .Select(i => i.Roles)
                            .Distinct()
                            .OrderBy(i => i));

                    if (!string.IsNullOrEmpty(rolesStr))
                    {
                        operation.Summary += $" {{Roles: {rolesStr}}}";
                        operation.Summary = operation.Summary.Trim();
                    }
                }

                // Polities to summary
                if (!_registerSwaggerOptions.DisablePoliciesInfo)
                {
                    var policiesStr = string.Join(", ",
                        authorizedAttributes
                            .Where(i => !string.IsNullOrEmpty(i.Policy))
                            .Select(i => i.Policy)
                            .Distinct()
                            .OrderBy(i => i));

                    if (!string.IsNullOrEmpty(policiesStr))
                    {
                        operation.Summary += $" {{Policies: {policiesStr}}}";
                        operation.Summary = operation.Summary.Trim();
                    }
                }

                operation.Security = new List<OpenApiSecurityRequirement>
                {
                    //Add JWT bearer type
                    new()
                    {
                        {
                            GetBearerSecurityScheme(),
                            Array.Empty<string>()
                        }
                    }
                };
            }
        }

        /// <summary>
        /// Get Bearer SecurityScheme
        /// </summary>
        /// <returns></returns>
        // ReSharper disable once UnusedMember.Global
        public static OpenApiSecurityScheme GetBearerSecurityScheme()
        {
            var result = new OpenApiSecurityScheme
            {
                Description = "Please insert JWT with Bearer into field. Example: \"Bearer MyAccessToken12345\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,

                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            return result;
        }
    }
}
