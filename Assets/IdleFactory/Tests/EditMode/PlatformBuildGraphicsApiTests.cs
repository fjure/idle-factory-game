using System.IO;
using IdleFactory.Editor;
using NUnit.Framework;

namespace IdleFactory.Tests.EditMode
{
    /// <summary>
    /// T056: the GLES3 emulator build must fail when <c>ProjectSettings.asset</c> keeps a non-automatic Android
    /// Graphics API, so the emulator-only override cannot leak into release configuration.
    /// </summary>
    public sealed class PlatformBuildGraphicsApiTests
    {
        private const string Header = "PlayerSettings:\n  m_BuildTargetIcons: []\n";
        private const string Footer = "  m_DefaultShaderChunkSizeInMB: 16\n  openGLRequireES31: 0\n";

        [Test]
        public void EmptyGraphicsApiListIsAutomatic()
        {
            var text = Header + "  m_BuildTargetGraphicsAPIs: []\n" + Footer;

            Assert.That(PlatformBuildCommands.FindNonAutomaticGraphicsApiTargets(text), Is.Empty);
        }

        [Test]
        public void RestoredAutomaticAndroidEntryIsAccepted()
        {
            var text = Header +
                "  m_BuildTargetGraphicsAPIs:\n" +
                "  - m_BuildTarget: AndroidPlayer\n" +
                "    m_APIs: 150000000b000000\n" +
                "    m_Automatic: 1\n" +
                Footer;

            Assert.That(PlatformBuildCommands.FindNonAutomaticGraphicsApiTargets(text), Is.Empty);
        }

        [Test]
        public void LeakedGles3OnlyAndroidOverrideIsReported()
        {
            var text = Header +
                "  m_BuildTargetGraphicsAPIs:\n" +
                "  - m_BuildTarget: iOSSupport\n" +
                "    m_APIs: 10000000\n" +
                "    m_Automatic: 1\n" +
                "  - m_BuildTarget: AndroidPlayer\n" +
                "    m_APIs: 0b000000\n" +
                "    m_Automatic: 0\n" +
                Footer;

            Assert.That(PlatformBuildCommands.FindNonAutomaticGraphicsApiTargets(text), Is.EqualTo(new[] { "AndroidPlayer" }));
        }

        [Test]
        public void WindowsLineEndingsAreParsed()
        {
            var text = (Header +
                "  m_BuildTargetGraphicsAPIs:\n" +
                "  - m_BuildTarget: AndroidPlayer\n" +
                "    m_APIs: 0b000000\n" +
                "    m_Automatic: 0\n" +
                Footer).Replace("\n", "\r\n");

            Assert.That(PlatformBuildCommands.FindNonAutomaticGraphicsApiTargets(text), Is.EqualTo(new[] { "AndroidPlayer" }));
        }

        [Test]
        public void KeysAfterTheGraphicsApiListAreIgnored()
        {
            var text = Header +
                "  m_BuildTargetGraphicsAPIs:\n" +
                "  - m_BuildTarget: AndroidPlayer\n" +
                "    m_APIs: 150000000b000000\n" +
                "    m_Automatic: 1\n" +
                "  m_BuildTargetVRSettings:\n" +
                "  - m_BuildTarget: AndroidPlayer\n" +
                "    m_Automatic: 0\n" +
                Footer;

            Assert.That(PlatformBuildCommands.FindNonAutomaticGraphicsApiTargets(text), Is.Empty);
        }

        [Test]
        public void CommittedProjectSettingsUseAutomaticAndroidGraphicsApis()
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "ProjectSettings", "ProjectSettings.asset");

            Assert.That(PlatformBuildCommands.FindNonAutomaticGraphicsApiTargets(File.ReadAllText(path)),
                Does.Not.Contain("AndroidPlayer"),
                "ProjectSettings.asset must not carry the emulator-only GLES3 Android override.");
        }
    }
}
