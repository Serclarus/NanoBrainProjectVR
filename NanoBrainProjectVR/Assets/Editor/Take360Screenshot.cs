using UnityEngine;
using UnityEditor;
using System.IO;

public class Take360Screenshot : ScriptableWizard
{
    public Camera renderCamera;
    public int imageResolution = 4096;
    public string saveName = "Skybox_Preview";

    [MenuItem("Tools/Take 360 VR Screenshot")]
    static void CreateWizard()
    {
        ScriptableWizard.DisplayWizard<Take360Screenshot>("Take 360 VR Screenshot", "Capture!");
    }

    private void OnWizardCreate()
    {
        if (renderCamera == null)
        {
            Debug.LogError("Please assign a camera!");
            return;
        }

        // 1. Create a Cubemap RenderTexture
        RenderTexture cubemapRT = new RenderTexture(imageResolution, imageResolution, 24, RenderTextureFormat.ARGB32);
        cubemapRT.dimension = UnityEngine.Rendering.TextureDimension.Cube;
        
        // 2. Render to it
        renderCamera.RenderToCubemap(cubemapRT);
        
        // 3. Create a 2D RenderTexture for the Equirectangular result
        RenderTexture equirectRT = new RenderTexture(imageResolution, imageResolution / 2, 24, RenderTextureFormat.ARGB32);
        
        // 4. Convert!
        cubemapRT.ConvertToEquirect(equirectRT);
        
        // 5. Read pixels into a Texture2D to save as PNG
        RenderTexture.active = equirectRT;
        Texture2D tex = new Texture2D(imageResolution, imageResolution / 2, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, imageResolution, imageResolution / 2), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        // Save the file
        byte[] bytes = tex.EncodeToPNG();
        string path = Application.dataPath + "/" + saveName + ".png";
        File.WriteAllBytes(path, bytes);
        
        // Clean up
        DestroyImmediate(cubemapRT);
        DestroyImmediate(equirectRT);
        DestroyImmediate(tex);

        Debug.Log($"[Success] 360 Screenshot saved to: {path}");
        AssetDatabase.Refresh();
    }
}
