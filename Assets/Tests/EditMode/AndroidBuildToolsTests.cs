using Coika.Tools;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Tests the Android build checks: manifest permissions, size report helpers and the pixel-art texture overrides
    /// (issue #38).
    /// </summary>
    public class AndroidBuildToolsTests
    {
        private const string AAPT2_OUTPUT =
            "package: com.acortescab.coika\n" +
            "uses-permission: name='android.permission.VIBRATE'\n" +
            "uses-permission-sdk-23: name='android.permission.INTERNET'\n" +
            "uses-permission: name='android.permission.VIBRATE'\n";

        /// <summary>
        /// Permissions are read from every uses-permission flavor and listed once.
        /// </summary>
        [Test]
        public void ParsePermissions_Aapt2Output_ReturnsDistinctNames()
        {
            var permissions = AndroidManifestCheck.ParsePermissions(AAPT2_OUTPUT);

            CollectionAssert.AreEqual(new[] { "android.permission.VIBRATE", "android.permission.INTERNET" }, permissions);
        }

        /// <summary>
        /// A manifest with only VIBRATE has no problem.
        /// </summary>
        [Test]
        public void FindProblems_OnlyVibrate_ReturnsNone()
        {
            var problems = AndroidManifestCheck.FindProblems(new[] { AndroidManifestCheck.REQUIRED_PERMISSION });

            Assert.IsEmpty(problems);
        }

        /// <summary>
        /// An INTERNET permission is reported, which fails the build.
        /// </summary>
        [Test]
        public void FindProblems_InternetAdded_ReportsIt()
        {
            var problems = AndroidManifestCheck.FindProblems(new[] { AndroidManifestCheck.REQUIRED_PERMISSION, AndroidManifestCheck.FORBIDDEN_PERMISSION });

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("INTERNET", problems[0]);
        }

        /// <summary>
        /// A manifest without VIBRATE is reported.
        /// </summary>
        [Test]
        public void FindProblems_VibrateMissing_ReportsIt()
        {
            var problems = AndroidManifestCheck.FindProblems(new string[0]);

            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("VIBRATE", problems[0]);
        }

        /// <summary>
        /// The group is the bundle name before the asset marker and the content hash.
        /// </summary>
        [TestCase("ui_assets_all_0a1b2c.bundle", "ui")]
        [TestCase("core-data_assets_all_ff00.bundle", "core-data")]
        [TestCase("scenes_scenes_all_12ab.bundle", "scenes")]
        [TestCase("unitybuiltinshaders_abcd.bundle", "unitybuiltinshaders_abcd")]
        public void GroupNameOf_BundleFileName_ReturnsGroup(string fileName, string expected)
        {
            Assert.AreEqual(expected, BuildSizeReport.GroupNameOf(fileName));
        }

        /// <summary>
        /// Sizes print in megabytes with two decimals.
        /// </summary>
        [Test]
        public void FormatMegabytes_OneAndAHalfMegabytes_PrintsTwoDecimals()
        {
            Assert.AreEqual("1.50 MB", BuildSizeReport.FormatMegabytes(1572864));
        }

        /// <summary>
        /// Every sprite texture has the uncompressed Android override and point filtering (GDD §19 Pixel quality).
        /// </summary>
        [Test]
        public void SpriteTextures_AndroidOverride_AreUncompressedPointFiltered()
        {
            CollectionAssert.IsEmpty(SpriteAndroidOverrides.FindNonCompliant());
        }
    }
}
