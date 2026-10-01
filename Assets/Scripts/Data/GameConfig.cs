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
        [SerializeField, Range(1, SpawnSettings.MAX_TIER_COUNT)]
        private int _spawnableTierCount = 5;
        [SerializeField, Min(1)]
        private int _antiStreakMax = 3;
        [SerializeField]
        private int[] _forcedOpeningTiers = { 0, 1, 0 };
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

        /// <summary>Interior size of the jar in world units (width, height). The Danger Line sits at the height.</summary>
        public Vector2 JarSize => _jarSize;

        /// <summary>Distance in world units from the Danger Line up to the Drop Line, where pieces are held.</summary>
        public float DropLineOffset => _dropLineOffset;

        /// <summary>Physics material of the jar walls and floor.</summary>
        public PhysicsMaterial2D WallMaterial => _wallMaterial;

        /// <summary>Physics material of the pieces.</summary>
        public PhysicsMaterial2D PieceMaterial => _pieceMaterial;

        /// <summary>Speed in units per second below which a piece counts as settled.</summary>
        public float SettledVelocity => _settledVelocity;

        /// <summary>Gravity of the world in units per second squared (negative is down).</summary>
        public float Gravity => _gravity;

        /// <summary>Seconds after releasing a piece before the next one is attached and controllable.</summary>
        public float DropCooldown => _dropDownCooldown;

        /// <summary>Seconds a settled piece must stay above the Danger Line before the game ends.</summary>
        public float OverflowTime => _overflowTime;

        /// <summary>Seconds after a piece is created by a merge during which it does not count for the overflow.</summary>
        public float OverflowGrace => _overflowGrace;

        /// <summary>Seconds within which a new merge continues the combo.</summary>
        public float ComboWindow => _comboWindow;

        /// <summary>Amount the combo multiplier grows with each merge of the combo.</summary>
        public float ComboStep => _comboStep;

        /// <summary>Highest combo multiplier.</summary>
        public int ComboCap => _comboCap;

        /// <summary>Points awarded when two Black Holes touch and vanish.</summary>
        public int SuperNovaBonus => _superNovaBonus;

        /// <summary>
        /// Logs an error for every invalid spawn value whenever the asset is edited, so a bad config is seen in the
        /// Editor and not when a run starts. It changes no value (S-31).
        /// </summary>
        private void OnValidate()
        {
            foreach (var error in SpawnSettings.Validate(_spawnWeights, _spawnableTierCount, _antiStreakMax, _forcedOpeningTiers))
            {
                Debug.LogError($"{name}: {error}", this);
            }
        }

        /// <summary>
        /// Copies the spawn values (weights, spawnable tier count, anti-streak maximum and forced opening) into an
        /// immutable <see cref="SpawnSettings"/> for the spawn queue. It allocates, so call it once per run.
        /// </summary>
        /// <returns>The validated settings.</returns>
        /// <exception cref="System.ArgumentException">The config holds invalid spawn values; the message lists them all.</exception>
        public SpawnSettings CreateSpawnSettings()
        {
            return new SpawnSettings(_spawnWeights, _spawnableTierCount, _antiStreakMax, _forcedOpeningTiers);
        }
    }
}
