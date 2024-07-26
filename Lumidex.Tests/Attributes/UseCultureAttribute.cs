using System.Globalization;
using System.Reflection;
using Xunit.Sdk;

namespace Lumidex.Tests.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class UseCultureAttribute : BeforeAfterTestAttribute
{
    private readonly Lazy<CultureInfo> _culture;
    private readonly Lazy<CultureInfo> _uiCulture;
    private CultureInfo _originalCulture = null!;
    private CultureInfo _originalUICulture = null!;

    public UseCultureAttribute(string culture)
        : this(culture, culture) { }

    public UseCultureAttribute(string culture, string uiCulture)
    {
        _culture = new(() => new CultureInfo(culture, false));
        _uiCulture = new(() => new CultureInfo(uiCulture, false));
    }

    public CultureInfo Culture => _culture.Value;

    public CultureInfo UICulture => _uiCulture.Value;

    public override void Before(MethodInfo methodUnderTest)
    {
        _originalCulture = Thread.CurrentThread.CurrentCulture;
        _originalUICulture = Thread.CurrentThread.CurrentUICulture;

        Thread.CurrentThread.CurrentCulture = Culture;
        Thread.CurrentThread.CurrentUICulture = UICulture;

        CultureInfo.CurrentCulture.ClearCachedData();
        CultureInfo.CurrentUICulture.ClearCachedData();
    }

    public override void After(MethodInfo methodUnderTest)
    {
        Thread.CurrentThread.CurrentCulture = _originalCulture;
        Thread.CurrentThread.CurrentUICulture = _originalUICulture;

        CultureInfo.CurrentCulture.ClearCachedData();
        CultureInfo.CurrentUICulture.ClearCachedData();
    }
}
