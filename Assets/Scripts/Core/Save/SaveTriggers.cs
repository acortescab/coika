using UnityEngine;

namespace Coika.Core
{
    /// <summary>
    /// Raises the save moments the game cannot see itself: end of frame (to flush debounced requests),
    /// app pause and app quit.
    /// </summary>
    [DisallowMultipleComponent]
    public class SaveTriggers : MonoBehaviour
    {
        private SaveSystem _save;

        /// <summary>
        /// Connects the component to the save system.
        /// </summary>
        /// <param name="save">System to flush.</param>
        public void Initialize(SaveSystem save)
        {
            _save = save;
        }

        /// <summary>
        /// Writes the pending changes once per frame, however many were requested.
        /// </summary>
        private void LateUpdate()
        {
            _save?.FlushIfDirty();
        }

        /// <summary>
        /// Saves when the app goes to the background.
        /// </summary>
        /// <param name="paused">True when the app is pausing.</param>
        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _save?.Save();
            }
        }

        /// <summary>
        /// Saves before the app closes.
        /// </summary>
        private void OnApplicationQuit()
        {
            _save?.Save();
        }
    }
}
