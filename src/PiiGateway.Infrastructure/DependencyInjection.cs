using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Core.Interfaces.Services;
using PiiGateway.Infrastructure.Data;
using PiiGateway.Infrastructure.Data.Converters;
using PiiGateway.Infrastructure.Options;
using PiiGateway.Infrastructure.Repositories;
using PiiGateway.Infrastructure.Services;
using PiiGateway.Infrastructure.Services.Extractors;

namespace PiiGateway.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<PiiServiceOptions>(configuration.GetSection(PiiServiceOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<EncryptionOptions>(configuration.GetSection(EncryptionOptions.SectionName));
        services.Configure<DataRetentionOptions>(configuration.GetSection(DataRetentionOptions.SectionName));
        services.Configure<GuestDemoOptions>(configuration.GetSection(GuestDemoOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        // Encryption (singleton — must be registered before DbContext so the converter can use it)
        services.AddSingleton<IEncryptionService, AesGcmEncryptionService>();

        // Database
        services.AddDbContext<PiiGatewayDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("PostgreSQL")));

        // Redis
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration.GetConnectionString("Redis");
        });

        // Repositories
        services.AddScoped<IJobRepository, JobRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<ITextSegmentRepository, TextSegmentRepository>();
        services.AddScoped<IPiiEntityRepository, PiiEntityRepository>();

        // Services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<AuditLogService>();
        services.AddScoped<IFileStorageService, FileStorageService>();
        services.AddScoped<IDocumentProcessor, DocumentProcessor>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IPseudonymizationService, PseudonymizationService>();
        services.AddScoped<ISecondScanService, SecondScanService>();
        services.AddScoped<IDePseudonymizationService, DePseudonymizationService>();
        services.AddScoped<IDocumentPreviewService, DocumentPreviewService>();

        // Document extractors (PlainTextExtractor must be last — it's the fallback)
        services.AddScoped<IDocumentExtractor, PdfExtractor>();
        services.AddScoped<IDocumentExtractor, DocxExtractor>();
        services.AddScoped<IDocumentExtractor, XlsxExtractor>();
        services.AddScoped<IDocumentExtractor, PlainTextExtractor>();

        // Job cancellation registry (singleton — shared between controller and document processor)
        services.AddSingleton<JobCancellationRegistry>();

        // LLM scan (singleton — holds in-memory scan state)
        services.AddSingleton<ILlmScanService, LlmScanService>();

        // Rescan service (singleton — holds in-memory rescan state)
        services.AddSingleton<RescanService>();

        // Playground usage tracker (singleton — tracks daily per-IP usage)
        services.AddSingleton<PlaygroundUsageTracker>();

        // Background processing queue (singleton — shared between controller and background service)
        services.AddSingleton<DocumentProcessingQueue>();
        services.AddSingleton<IDocumentProcessingQueue>(sp => sp.GetRequiredService<DocumentProcessingQueue>());
        services.AddHostedService<DocumentProcessingBackgroundService>();
        services.AddHostedService<DataRetentionBackgroundService>();

        // HTTP Client for PII detection service
        services.AddHttpClient<IPiiDetectionClient, PiiDetectionClient>();

        // Email service
        services.AddScoped<IEmailService, SmtpEmailService>();

        // Data encryption migration (for encrypting existing plaintext records)
        services.AddTransient<DataEncryptionMigrationService>();

        return services;
    }

    /// <summary>
    /// Initializes the static EncryptedStringConverter with the registered IEncryptionService.
    /// Must be called after the service provider is built but before any DbContext usage.
    /// </summary>
    public static void InitializeEncryption(IServiceProvider serviceProvider)
    {
        var encryptionService = serviceProvider.GetRequiredService<IEncryptionService>();
        EncryptedStringConverter.SetEncryptionService(encryptionService);
    }
}
