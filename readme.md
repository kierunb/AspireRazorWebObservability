# Observability with OpenTelemetry samples

## Developer experience

- [OpenTelemetry for .NET](https://opentelemetry.io/docs/languages/net/instrumentation/)

### Jaeger with OpenTelemetry (docker)

```shell
docker run --name jaeger `
  -p 16686:16686 `
  -p 4317:4317 `
  -p 4318:4318 `
  -p 5778:5778 `
  -p 9411:9411 `
  jaegertracing/jaeger:2.3.0
```

### Standalone .NET Aspire dashboard (docker)

```shell
docker run -it -d `
	-p 18888:18888 `
	-p 4317:18889 `
	--name aspire-dashboard `
	mcr.microsoft.com/dotnet/aspire-dashboard
```

- [more information](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone?tabs=powershell)

### SEQ

```shell
docker run -d `
  --name seq `
  --restart unless-stopped `
  -e ACCEPT_EULA=Y `
  -v seq:/data `
  -p 5341:80 `
  datalust/seq
```

- [more information](https://docs.datalust.co/docs/opentelemetry-net-sdk)

## AppInsights (Production monitoring)

Azure AppInsights monitoring configuration:

```shell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:AppInsights" ""
dotnet user-secrets set "ConnectionStrings:StorageAccount" ""
```

### References

#### OpenTelemetry
https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-enable?tabs=aspnetcore
https://learn.microsoft.com/en-us/azure/azure-monitor/app/opentelemetry-configuration?tabs=aspnetcore

#### .NET Diagnostics
https://learn.microsoft.com/en-us/dotnet/core/diagnostics/

#### Minimal APIs
https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-9.0
https://www.mikesdotnetting.com/article/358/using-minimal-apis-in-asp-net-core-razor-pages
https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis/responses?view=aspnetcore-9.0

#### Caching
https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output?view=aspnetcore-9.0
https://learn.microsoft.com/en-us/aspnet/core/performance/caching/response?view=aspnetcore-9.0

#### Base url
https://freedium.cfd/https://medium.com/it-dead-inside/accessing-the-asp-net-core-base-url-so-many-ways-f07f6ee58f68

#### Streams
https://medium.com/@dmytro.misik/net-streams-f3e9801b7ef0

#### Azure FrontDoor/CDN
https://learn.microsoft.com/en-us/azure/frontdoor/front-door-overview
https://learn.microsoft.com/en-us/azure/frontdoor/front-door-caching?pivots=front-door-standard-premium
https://learn.microsoft.com/en-us/azure/architecture/web-apps/guides/enterprise-app-patterns/modern-web-app/dotnet/guidance