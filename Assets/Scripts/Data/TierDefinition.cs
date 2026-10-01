using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Coika.Data
{
    /// <summary>
    /// Immutable data of one piece tier (GDD §3.2). The sprite is an Addressable reference (C-01).
    /// </summary>
    [CreateAssetMenu(fileName = "TierDefinition", menuName = "Scriptable Objects/Tier Definitions")]
    public class TierDefinition : ScriptableObject
    {
        /// <summary>Sprite pixels per world unit; a sprite is as many pixels wide as its diameter times this value.</summary>
        public const float PixelsPerUnit = 16f;

        [SerializeField]
        private int _index;
        [SerializeField]
        private string _displayName;
        [SerializeField]
        private AssetReferenceSprite _sprite;
        [SerializeField]
        private float _diameterUnits;
        [SerializeField]
        private float _radius;
        [SerializeField]
        private float _mergeScore;
        [SerializeField]
        private Color _tierColor;
        [SerializeField]
        private string _loreLine; // Localization key, e.g. tier.03.lore

        /// <summary>Position of the tier in the evolution chain, from 0 (smallest) to 10.</summary>
        public int Index => _index;
        public string DisplayName => _displayName;

        /// <summary>Addressable reference to the tier sprite. Load it through IAssetService.</summary>
        public AssetReferenceSprite Sprite => _sprite;
        public float DiameterUnits => _diameterUnits;

        /// <summary>Collider radius in units. Always half of <see cref="DiameterUnits"/>.</summary>
        public float Radius => _radius;

        /// <summary>Points awarded when a merge produces a piece of this tier.</summary>
        public float MergeScore => _mergeScore;
        public Color TierColor => _tierColor;

        /// <summary>Localization key of the lore line.</summary>
        public string LoreLine => _loreLine;

        /// <summary>
        /// Keeps the serialized radius in sync with the diameter whenever the asset is edited.
        /// </summary>
        private void OnValidate()
        {
            _radius = _diameterUnits * 0.5f;
        }
    }
}
