using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.XR.ARFoundation.Samples
{
    public class DragModeController : MonoBehaviour
    {
        public Material transparentBlueMat;
        public string scriptNameToDisable = "DrumHitHandler";
        public string instrumentTag = "Instrument";
        private bool isTransparentMode = false;

        void Start()
    {
        InstrumentGlobalState.transparentBlueMat = transparentBlueMat;
        InstrumentGlobalState.scriptNameToDisable = scriptNameToDisable;
    }

        public void ToggleInstrumentTransparency()
        {
            isTransparentMode = !isTransparentMode;
            InstrumentGlobalState.isTransparentMode = !InstrumentGlobalState.isTransparentMode;

            var allInstruments = GameObject.FindGameObjectsWithTag(instrumentTag);

            foreach (var go in allInstruments)
            {
                var keeper = go.GetComponent<InstrumentMaterialKeeper>();
                if (keeper == null) continue;

                // 多Renderer处理和材质切换
                if (isTransparentMode)
                {
                    keeper.SetAllRenderersMaterial(transparentBlueMat);
                    var mb = go.GetComponent(scriptNameToDisable) as MonoBehaviour;
                    if (mb != null)
                        mb.enabled = false;
                }
                else
                {
                    keeper.RestoreMaterials();
                    var mb = go.GetComponent(scriptNameToDisable) as MonoBehaviour;
                    if (mb != null)
                        mb.enabled = true;
                }
            }
        }
    }
}
