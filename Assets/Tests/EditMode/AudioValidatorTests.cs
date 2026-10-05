using Coika.Tools;
using NUnit.Framework;

namespace Coika.Tests.EditMode
{
    /// <summary>
    /// Checks the shipped audio against the import settings of GDD §11 and the Addressables rules of C-01 (issue #29).
    /// </summary>
    public class AudioValidatorTests
    {
        /// <summary>
        /// Every clip and the mixer have the import settings, groups, labels and names the game expects.
        /// </summary>
        [Test]
        public void ShippedAudio_Always_PassesTheValidator()
        {
            var errors = AudioValidator.GetErrors();

            Assert.IsEmpty(errors, string.Join("\n", errors));
        }
    }
}
