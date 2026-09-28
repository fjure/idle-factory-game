using IdleFactory.Editor;
using NUnit.Framework;
using UnityEditor;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T064: the GLES3 emulator build keeps the profiler autoconnect by default and omits it only with
    /// <c>-noProfilerAutoconnect</c>, the build for the Unity Profiler (Standalone Process).
    /// </summary>
    public sealed class PlatformBuildProfilerOptionsTests
    {
        [Test]
        public void DefaultDevelopmentBuildConnectsWithProfiler()
        {
            Assert.That(PlatformBuildCommands.AndroidDevelopmentBuildOptions(true),
                Is.EqualTo(BuildOptions.Development | BuildOptions.ConnectWithProfiler));
        }

        [Test]
        public void StandaloneProfilerBuildIsDevelopmentWithoutAutoconnect()
        {
            var options = PlatformBuildCommands.AndroidDevelopmentBuildOptions(false);

            Assert.That(options, Is.EqualTo(BuildOptions.Development));
            Assert.That(options & BuildOptions.ConnectWithProfiler, Is.EqualTo(BuildOptions.None));
        }

        [Test]
        public void NoProfilerAutoconnectArgumentIsDetectedExactly()
        {
            var name = PlatformBuildCommands.NoProfilerAutoconnectArgument;

            Assert.That(PlatformBuildCommands.HasArgument(new[] { "Unity.exe", "-batchmode", name, "-buildOutput", "a.apk" }, name), Is.True);
            Assert.That(PlatformBuildCommands.HasArgument(new[] { "Unity.exe", "-batchmode", "-buildOutput", "a.apk" }, name), Is.False);
            Assert.That(PlatformBuildCommands.HasArgument(new[] { name + "=0" }, name), Is.False);
            Assert.That(PlatformBuildCommands.HasArgument(null, name), Is.False);
        }
    }
}
