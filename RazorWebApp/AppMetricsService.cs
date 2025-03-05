using System.Diagnostics.Metrics;

namespace RazorWebApp;

public class AppMetricsService
{
    private readonly Counter<int> _userClicks;
    private readonly Counter<int> _indexPageVisitsCounter;
    private readonly Histogram<double> _responseTime;

    private int _requests;
    private double _memoryConsumption;

    public AppMetricsService(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("App.Frontend");

        _userClicks = meter.CreateCounter<int>("app.frontend.user_clicks");
        _indexPageVisitsCounter = meter.CreateCounter<int>("app.frontend.index_page_visits");
        _responseTime = meter.CreateHistogram<double>(
            name: "app.frontend.response_time",
            unit: "Seconds",
            description: "This metric measures the time taken for the application to respond to user requests.");

        meter.CreateObservableCounter("app.frontend.requests", () => _requests);
        meter.CreateObservableGauge(
            name: "app.frontend.memory_consumption",
            observeValue: () => _memoryConsumption,
            unit: "Megabytes",
            description: "This metric measures the amount of memory used by the application.");
    }

    public void RecordUserClick()
    {
        _userClicks.Add(1);
    }

    public void RecordUserClickDetailed(string region, string feature)
    {
        _userClicks.Add(1,
            new KeyValuePair<string, object?>("user.region", region),
            new KeyValuePair<string, object?>("user.feature", feature));
    }

    public void PageVisit(int quantity)
    {
        _indexPageVisitsCounter.Add(quantity);
    }

    public void RecordResponseTime(double value)
    {
        _responseTime.Record(value);
    }
    public void RecordRequest()
    {
        Interlocked.Increment(ref _requests);
    }
    public void RecordMemoryConsumption(double value)
    {
        _memoryConsumption = value;
    }
}
