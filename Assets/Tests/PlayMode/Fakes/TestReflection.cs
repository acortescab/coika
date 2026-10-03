using System.Reflection;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Reflection helper of the PlayMode tests: the data classes (<c>GameConfig</c>, <c>TierDefinition</c>) are
    /// immutable ScriptableObjects with no public setters (S-31), so a test sets their private fields.
    /// </summary>
    public static class TestReflection
    {
        /// <summary>
        /// Sets a private instance field by reflection.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the private field.</param>
        /// <param name="value">Value to assign.</param>
        public static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(target, value);
        }
    }
}
