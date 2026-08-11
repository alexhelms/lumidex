using System.Reflection;
using System.Runtime.InteropServices;

namespace Lumidex.Tests;

// Regression guard for the cfitsio native-load bug on published Linux builds.
//
// The DllImportResolver must build its path from the PORTABLE RID whose
// runtimes/<rid>/native/ folder we actually ship (linux-x64, osx-arm64,
// osx-x64, win-x64) — NOT from RuntimeInformation.RuntimeIdentifier, which on
// Linux is the distro-specific RID (e.g. "fedora.44-x64", "ubuntu.24.04-x64").
// That distro RID has no matching runtimes/ folder, so the resolver pointed at
// a path that never exists and cfitsio failed to load on installed builds.
//
// Bootstrapper.GetNativeRuntimeIdentifier is private. We reach it by reflection
// rather than widening its visibility, so the production diff stays limited to
// the resolver fix itself.
public class NativeLibraryResolverTests
{
    [Fact]
    public void GetNativeRuntimeIdentifier_TargetsAShippedPortableRid()
    {
        // Fully qualified: there is also a Lumidex.Bootstrapper in the app
        // assembly, and an unqualified name binds to the wrong one.
        var method = typeof(Lumidex.Core.Bootstrapper).GetMethod(
            "GetNativeRuntimeIdentifier",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("the resolver must map OS + architecture to a portable RID");

        var rid = (string?)method!.Invoke(null, parameters: null);

        // The four RIDs Lumidex.Core ships a native cfitsio under.
        string[] shippedRids = ["linux-x64", "osx-arm64", "osx-x64", "win-x64"];
        rid.Should().BeOneOf(shippedRids);

        // The field failure was Linux-only: linux-x64 is the single Linux folder
        // we ship, so the distro-specific RID must never be what gets used. This
        // assertion holds on every Linux distro (it asserts the portable result,
        // not the absence of any particular distro string).
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            rid.Should().Be("linux-x64");
    }

    // Guards the SECOND half of the same fix: the load path must be anchored at
    // the app base directory, not the current working directory. The field bug
    // had two compounding causes — the wrong RID (above) and a "./"-relative
    // path that missed whenever an installed build was launched from a CWD other
    // than its own folder. A regression reverting only the anchoring would leave
    // the RID test green, so it needs its own assertion.
    [Fact]
    public void GetNativeLibraryPath_IsRootedAtAppBaseUnderTheShippedRid()
    {
        var method = typeof(Lumidex.Core.Bootstrapper).GetMethod(
            "GetNativeLibraryPath",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull("the resolver must anchor the native path at the app base directory");

        var path = (string?)method!.Invoke(null, parameters: ["cfitsio"]);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            path.Should().NotBeNull();
            Path.IsPathRooted(path).Should().BeTrue("a CWD-relative path is exactly the bug being fixed");
            path!.Should().StartWith(AppContext.BaseDirectory);
            path.Should().Contain(Path.Combine("runtimes", "linux-x64", "native"));
            path.Should().EndWith("libcfitsio.so");
        }
    }
}
