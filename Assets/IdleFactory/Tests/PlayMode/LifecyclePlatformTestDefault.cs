#if UNITY_EDITOR
using System;
using IdleFactory.Unity.Bootstrap;
using NUnit.Framework;

/// <summary>
/// T250: the Play Mode suites run inside the editor, which the lifecycle treats as a desktop (focus loss is not a departure).
/// The lifecycle, rotation, short-return and host tests were written for the phone and tablet behaviour (Feature 001
/// FR-037, R20, T246), so the whole assembly runs as a mobile platform unless a test asks for the desktop class through
/// <see cref="LifecyclePlatform.Override"/> (see <c>RunDesktopLifecycleTests</c>). The tagged player suite is built without
/// this fixture and runs with the platform's real class.
/// </summary>
[SetUpFixture]
public sealed class LifecyclePlatformTestDefault
{
    private IDisposable scope;

    [OneTimeSetUp]
    public void RunAsMobilePlatform()
    {
        scope = LifecyclePlatform.Override(true);
    }

    [OneTimeTearDown]
    public void RestoreRuntimePlatform()
    {
        scope?.Dispose();
        scope = null;
    }
}
#endif
