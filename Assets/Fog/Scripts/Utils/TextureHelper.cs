using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace VolumetricFogExtension
{

    [System.Serializable]
    public struct DTexture3D
    {
        public DTexture3D(int inX, int inY, int inZ)
        {
            x = inX;
            y = inY;
            z = inZ;
        }

        public int x;
        public int y;
        public int z;

        public static DTexture3D zero = new DTexture3D(0, 0, 0);
        public static DTexture3D one = new DTexture3D(1, 1, 1);
    }

    public static class TextureHelper
    {
        public static Texture3D CreateFogLUT3DFrom2DSlices(Texture2D tex, DTexture3D dimensions)
        {
            var readableTexture2D = GetReadableTexture(tex);
            var colors = new Color[dimensions.x * dimensions.y * dimensions.z];

            var idx = 0;

            for (var z = 0; z < dimensions.z; ++z)
            {
                for (var y = 0; y < dimensions.y; ++y)
                {
                    for (var x = 0; x < dimensions.x; ++x, ++idx)
                    {
                        colors[idx] = readableTexture2D.GetPixel(x + z * dimensions.z, y);
                    }
                }
            }

            var texture3D = new Texture3D(dimensions.x, dimensions.y, dimensions.z, TextureFormat.RGBAHalf, true);
            texture3D.SetPixels(colors);
            texture3D.Apply();

            return texture3D;
        }

        private static Texture2D GetReadableTexture(Texture2D texture)
        {
            // Create a temporary RenderTexture of the same size as the texture
            var tmpRenderTexture = RenderTexture.GetTemporary(texture.width, texture.height, 0,
                RenderTextureFormat.Default, RenderTextureReadWrite.Linear);

            // Blit the pixels on texture to the RenderTexture
            Graphics.Blit(texture, tmpRenderTexture);
            var previous = RenderTexture.active;
            RenderTexture.active = tmpRenderTexture;
            var myTexture2D = new Texture2D(texture.width, texture.height);
            myTexture2D.ReadPixels(new Rect(0, 0, tmpRenderTexture.width, tmpRenderTexture.height), 0, 0);
            myTexture2D.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(tmpRenderTexture);
            return myTexture2D;
        }
    }
}