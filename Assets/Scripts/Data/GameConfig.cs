using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Data
{
    /// <summary>
    /// Global tuning values of the game (every [TUNE] value of the GDD) plus the active theme.
    /// Immutable data at runtime: never write to its fields during play (S-31).
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Scriptable Objects/GameConfig")]
    public class GameConfig : ScriptableObject
    {
        [SerializeField]
        private float _gravity = -20f;
        [SerializeField]
        private float _dropDownCooldown = 0.5f;
        [SerializeField]
        private float _overflowTime = 2f;
        [SerializeField]
        private float _overflowGrace = 1f;
        [SerializeField]
        private float _settledVelocity = 0.2f;
        [SerializeField]
        private float _comboWindow = 1f;
        [SerializeField]
        private float _comboStep = 0.25f;
        [SerializeField]
        private int _comboCap = 3;
        [SerializeField]
        private float[] _spawnWeights = { 30f, 28f, 20f, 14f, 8f };
        [SerializeField]
        private int _spawnableTierCount = 5;
        [SerializeField]
        private int _antiStreakMax = 3;
        [SerializeField]
        private Vector3 _forcedOpeningTiers = new(0, 1, 0);
        [SerializeField]
        private Vector2 _jarSize = new(10f, 12.5f);
        [SerializeField]
        private float _dropLineOffset = 1.5f;
        [SerializeField]
        private int _superNovaBonus = 500;
        [SerializeField]
        private PhysicsMaterial2D _pieceMaterial;
        [SerializeField]
        private PhysicsMaterial2D _wallMaterial;
        [SerializeField]
        private AssetReferenceT<ThemeDefinition> _theme;

        /// <summary>Addressable reference to the active theme. Load it through IAssetService.</summary>
        public AssetReferenceT<ThemeDefinition> Theme => _theme;
    }
}
