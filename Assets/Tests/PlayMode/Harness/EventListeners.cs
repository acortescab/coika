using System;
using System.Reflection;

namespace Coika.Tests.PlayMode
{
    /// <summary>
    /// Counts the subscribers of a field-like event by reading its backing field, so a lifecycle test can prove that
    /// a run leaves no listener behind. Reflection is the only way: no production type exposes its subscriber counts.
    /// </summary>
    public static class EventListeners
    {
        /// <summary>
        /// Counts the subscribers of an event.
        /// </summary>
        /// <param name="owner">Object that declares the event.</param>
        /// <param name="eventName">Name of the event, which is also the name of its backing field.</param>
        /// <returns>The number of subscribers, 0 when there are none.</returns>
        /// <exception cref="ArgumentException">The owner has no field-like event with that name, so the test would count nothing.</exception>
        public static int Count(object owner, string eventName)
        {
            var field = owner.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new ArgumentException($"{owner.GetType().Name} has no field-like event '{eventName}'.", nameof(eventName));
            }

            var handler = (Delegate)field.GetValue(owner);
            return handler == null ? 0 : handler.GetInvocationList().Length;
        }
    }
}
