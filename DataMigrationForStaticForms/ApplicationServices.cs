using DataMigrationForStaticForms.GlobalOptions;
using DataMigrationForStaticForms.NTPFormCreation;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DataMigrationForStaticForms
{
    public static class ApplicationServices
    {
        public static IServiceCollection ConfigureServices(this IServiceCollection services)
        {
            services.AddScoped<IBooleanMappingProvider, BooleanMappingProvider>();
            services.AddScoped<GlobalOptionConfigurtion>();
            services.AddScoped<FormCreation>();


            return services;
        }
    }
}
