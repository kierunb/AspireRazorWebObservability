# Security Hotfix: XSS Vulnerability

## Critical Issue
The current implementation uses `@Html.Raw()` without sanitization, creating a critical XSS vulnerability.

## Immediate Fix Required

### 1. Install HTML Sanitizer Package
```bash
dotnet add package HtmlSanitizer
```

### 2. Add Sanitization Service

```csharp
// Add to Program.cs
builder.Services.AddScoped<IHtmlSanitizer>(provider =>
{
    var sanitizer = new HtmlSanitizer();
    
    // Configure allowed tags for HTML content
    sanitizer.AllowedTags.Clear();
    sanitizer.AllowedTags.UnionWith(new[] {
        "p", "div", "span", "h1", "h2", "h3", "h4", "h5", "h6",
        "strong", "em", "u", "br", "ul", "ol", "li", "a", "img"
    });
    
    // Configure allowed attributes
    sanitizer.AllowedAttributes.Clear();
    sanitizer.AllowedAttributes.Add("class");
    sanitizer.AllowedAttributes.Add("id");
    sanitizer.AllowedAttributes.Add("href");
    sanitizer.AllowedAttributes.Add("src");
    sanitizer.AllowedAttributes.Add("alt");
    
    // Only allow safe URL schemes
    sanitizer.AllowedSchemes.Clear();
    sanitizer.AllowedSchemes.UnionWith(new[] { "http", "https", "mailto" });
    
    return sanitizer;
});
```

### 3. Update PageModel

```csharp
private readonly IHtmlSanitizer _htmlSanitizer;

public HtmlBlobModel(
    ILogger<HtmlBlobModel> logger,
    BlobService blobService,
    HybridCache cache,
    BlobServiceClient blobServiceClient,
    IHtmlSanitizer htmlSanitizer)
{
    _logger = logger;
    _blobService = blobService;
    _cache = cache;
    _blobServiceClient = blobServiceClient;
    _htmlSanitizer = htmlSanitizer;
}

// Add sanitization method
public string GetSanitizedContent()
{
    if (string.IsNullOrEmpty(HtmlContent))
        return string.Empty;
        
    try
    {
        return _htmlSanitizer.Sanitize(HtmlContent);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error sanitizing HTML content");
        return "Content could not be displayed safely.";
    }
}
```

### 4. Update Razor View

Replace:
```html
@Html.Raw(Model.HtmlContent)
```

With:
```html
@Html.Raw(Model.GetSanitizedContent())
```

### 5. Add Input Validation

```csharp
private static readonly Regex ContainerNamePattern = new(@"^[a-z0-9](?:[a-z0-9\-]*[a-z0-9])?$", RegexOptions.Compiled);
private static readonly Regex BlobNamePattern = new(@"^[a-zA-Z0-9][a-zA-Z0-9\-_.]*\.(html|htm)$", RegexOptions.Compiled);

private bool ValidateInputs()
{
    if (string.IsNullOrWhiteSpace(ContainerName) || 
        string.IsNullOrWhiteSpace(BlobName))
    {
        ErrorMessage = "Container name and blob name are required.";
        return false;
    }

    if (!ContainerNamePattern.IsMatch(ContainerName))
    {
        ErrorMessage = "Invalid container name format.";
        return false;
    }

    if (!BlobNamePattern.IsMatch(BlobName))
    {
        ErrorMessage = "Invalid blob name format. Only HTML files are allowed.";
        return false;
    }

    if (ContainerName.Length > 63 || BlobName.Length > 1024)
    {
        ErrorMessage = "Container or blob name is too long.";
        return false;
    }

    return true;
}
```

## Testing the Fix

Create a test HTML file with malicious content:
```html
<script>alert('XSS Test')</script>
<p>Safe content</p>
<img src="x" onerror="alert('XSS')" />
```

After applying the fix, only the safe `<p>` tag should render, and scripts should be stripped.

## Priority: CRITICAL
This fix must be applied before any deployment to production.
