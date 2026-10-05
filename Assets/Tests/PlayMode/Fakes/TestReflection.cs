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
        /// Reads a private instance field by reflection, such as the backing delegate of an event.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the private field.</param>
        /// <returns>The value of the field.</returns>
        public static object GetField(object target, string fieldName)
        {
            return target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(target);
        }

        /// <summary>
        /// Builds a delegate for a private parameterless instance method, such as Unity's <c>LateUpdate</c>. Build it
        /// once before measuring, because calling it then allocates nothing, unlike <c>MethodInfo.Invoke</c>.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the private method.</param>
        /// <returns>An action that calls the method on the target.</returns>
        public static System.Action GetAction(object target, string methodName)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            return (System.Action)System.Delegate.CreateDelegate(typeof(System.Action), target, method);
        }

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
