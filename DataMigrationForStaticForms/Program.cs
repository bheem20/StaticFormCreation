
using Core365.EntityFrameworkCore;
using DataMigrationForStaticForms;
using DataMigrationForStaticForms.GlobalOptions;
using DataMigrationForStaticForms.NTPFormCreation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public class Program
{
    public static async Task Main()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddUserSecrets<Program>()
            .Build();

        string connectionString = configuration.GetConnectionString("DefaultConnection");


        var services = new ServiceCollection();

        services.AddDbContext<Core365DbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services.ConfigureServices();

        var serviceBuild = services.BuildServiceProvider();

        using (var scope = serviceBuild.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Core365DbContext>();

            var booleanMappingProvider = scope.ServiceProvider.GetRequiredService<IBooleanMappingProvider>();

            var globalOptionConfigurtion = scope.ServiceProvider.GetRequiredService<GlobalOptionConfigurtion>();

            var tenantList = dbContext.Tenants.Where(x => x.IsActive && !x.IsDeleted).Select(x => x.Id).ToList();

            tenantList = tenantList.Where(x => x == 3).ToList();

            foreach(var tenantId in tenantList)
            {
                var globalOptions = booleanMappingProvider.GetNTPMappingsWithDataBinding();

                await globalOptionConfigurtion.MigrateAsync(tenantId, globalOptions);

                var formCreation = scope.ServiceProvider.GetRequiredService<FormCreation>();
                formCreation.CreateForm();
            }

        }
    }
} 