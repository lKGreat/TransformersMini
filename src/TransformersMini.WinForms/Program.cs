using Microsoft.Extensions.DependencyInjection;
using TransformersMini.Contracts.Abstractions;
using TransformersMini.Infrastructure.DependencyInjection;

namespace TransformersMini.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var services = new ServiceCollection();
        services.AddTransformersMiniPlatform();
        using var provider = services.BuildServiceProvider();
        var runControl = provider.GetRequiredService<IRunControlService>();

        System.Windows.Forms.Application.Run(new MainForm(runControl));
    }
}
