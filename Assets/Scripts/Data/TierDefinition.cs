using UnityEngine;

namespace Coika.Core.Data
{
    [CreateAssetMenu(fileName = "TierDefinition", menuName = "Scriptable Objects/Tier Definitions")]
    public class TierDefinition : ScriptableObject
    {
        [SerializeField]
        private int _index;
        [SerializeField]
        private string _displayName;
        [SerializeField]
        private Sprite _sprite;
        [SerializeField]
        private float _diameterUnits;
        [SerializeField]
        private float _radius;
        [SerializeField]
        private float _mergeScore;
        [SerializeField]
        private Color _tierColor;
        [SerializeField]
        private string _loreLine;
    }
}
