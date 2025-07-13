using UnityEngine;

public class InstrumentMaterialKeeper : MonoBehaviour
{
    [HideInInspector] public Material[] originalMaterials;
    private MeshRenderer[] renderers;

    void Awake()
    {
        // 保存所有Renderer的材质
        renderers = GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length > 0)
        {
            // 如果有多个Renderer，拼成一个大数组
            var matList = new System.Collections.Generic.List<Material>();
            foreach (var r in renderers)
                matList.AddRange(r.materials);
            originalMaterials = matList.ToArray();
        }
    }

    public void SetAllRenderersMaterial(Material mat)
    {
        foreach (var renderer in renderers)
        {
            var mats = renderer.materials;
            for (int i = 0; i < mats.Length; i++)
                mats[i] = mat;
            renderer.materials = mats;
        }
    }

    public void RestoreMaterials()
    {
        // 将所有Renderer的材质恢复
        int matIdx = 0;
        foreach (var renderer in renderers)
        {
            var mats = renderer.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (matIdx < originalMaterials.Length)
                    mats[i] = originalMaterials[matIdx++];
            }
            renderer.materials = mats;
        }
    }
}