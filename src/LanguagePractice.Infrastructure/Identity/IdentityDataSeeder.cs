using LanguagePractice.Core.Constants;
using LanguagePractice.Core.Entities;
using LanguagePractice.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LanguagePractice.Infrastructure.Identity;

public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDataSeeder");
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var config = sp.GetRequiredService<IConfiguration>();

        if (!await roleManager.RoleExistsAsync(AppRoles.Admin))
        {
            await roleManager.CreateAsync(new IdentityRole(AppRoles.Admin));
            logger.LogInformation("Admin rolü oluşturuldu.");
        }

        var email = config["AdminSeed:Email"] ?? "admin@linguatalk.local";
        var password = config["AdminSeed:Password"] ?? "Admin123!";
        var displayName = config["AdminSeed:DisplayName"] ?? "Sistem Admin";

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                IsActive = true
            };

            var create = await userManager.CreateAsync(admin, password);
            if (!create.Succeeded)
            {
                logger.LogError("Admin kullanıcı oluşturulamadı: {Errors}",
                    string.Join(", ", create.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Varsayılan admin kullanıcı oluşturuldu: {Email}", email);
        }

        if (!await db.Profiles.AnyAsync(x => x.UserId == admin.Id))
        {
            db.Profiles.Add(new UserProfile { UserId = admin.Id });
            await db.SaveChangesAsync();
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin))
        {
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
            logger.LogInformation("Admin rolü kullanıcıya atandı: {Email}", email);
        }
    }
}
