using UnityEditor;

namespace Coika.Tools
{
    /// <summary>
    /// Imports every piece body and chart icon (piece_XX_name.png, icon_XX_name.png under Assets/Art) with the
    /// pixel-art settings of GDD §10, so a new piece texture starts with them. The EditMode suite catches a texture
    /// that was changed afterwards.
    /// </summary>
    public class SpriteImportPostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Unity calls this before it imports a texture; it applies <see cref="PieceArtRules.Apply"/> to piece art.
        /// </summary>
        private void OnPreprocessTexture()
        {
            if (!PieceArtRules.IsPieceArt(assetPath))
            {
                return;
            }

            PieceArtRules.Apply((TextureImporter)assetImporter);
        }
    }
}
