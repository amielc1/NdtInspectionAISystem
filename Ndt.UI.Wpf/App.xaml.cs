using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Ndt.Domain;
using Ndt.Infrastructure.AI;
using Ndt.Infrastructure.ImageProcessing;
using Ndt.UI.Wpf.ViewModels;
using Microsoft.SemanticKernel.Connectors.Google;

namespace Ndt.UI.Wpf
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private IConfiguration? _configuration;

        public App()
        {
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Core Services
            services.AddSingleton<IConfiguration>(_ => _configuration!);
            services.AddSingleton<IImageProcessor, NdtImageProcessor>();
        
            // AI Services
            services.AddSingleton<Kernel>(_ => 
            {
                var apiKey = _configuration!["AiSettings:GEMINI_API_KEY"];
                var modelId = _configuration!["AiSettings:ModelId"] ?? "gemini-2.5-flash-lite";

                var builder = Kernel.CreateBuilder();
                builder.AddGoogleAIGeminiChatCompletion(modelId: modelId, apiKey: apiKey!);
                return builder.Build();
            });
            services.AddSingleton<IAiAnalysisService, AiService>();

            // ViewModels
            services.AddTransient<MainViewModel>();

            // Views
            services.AddTransient<MainWindow>(s => new MainWindow
            {
                DataContext = s.GetRequiredService<MainViewModel>()
            });
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var mainWindow = _serviceProvider?.GetRequiredService<MainWindow>();
            mainWindow?.Show();
        }
    }
}

