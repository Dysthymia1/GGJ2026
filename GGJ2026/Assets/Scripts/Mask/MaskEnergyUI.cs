using TMPro;
using UnityEngine;

namespace Mask
{
    /// <summary>
    /// 能量框 UI：右上角显示当前剩余/总次数 x/y，随 MaskSystem 事件更新。
    /// </summary>
    public class MaskEnergyUI : MonoBehaviour
    {
        [SerializeField] private MaskSystem maskSystem;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text energyText;
        [Tooltip("格式中 {0}=剩余, {1}=总数，例如 \"{0}/{1}\"")]
        [SerializeField] private string format = "{0}/{1}";

        public void ToggleMaskEnergyUI(bool toggle)
        {
            root.SetActive(toggle);
        }

        private void OnEnable()
        {
            if (!maskSystem) return;
            maskSystem.OnEnergyChanged += OnEnergyChanged;
            Refresh(maskSystem.RemainingEnergy, maskSystem.TotalEnergy);
        }

        private void OnDisable()
        {
            if (maskSystem) {
                maskSystem.OnEnergyChanged -= OnEnergyChanged;
            }
        }

        private void OnEnergyChanged(int remaining, int total)
        {
            Refresh(remaining, total);
        }

        private void Refresh(int remaining, int total)
        {
            if (energyText) {
                energyText.text = string.Format(format, remaining, total);
            }
        }
    }
}
