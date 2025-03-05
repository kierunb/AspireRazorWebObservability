using System.Diagnostics;

namespace RazorWebApp;

public static class AppActivitySources
{
    private static ActivitySource appFrontentActivitySource = new("App.Frontend", "1.0.0");

    public static ActivitySource AppFrontentActivitySource { get => appFrontentActivitySource; }
}
