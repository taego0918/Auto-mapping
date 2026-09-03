using TMPro;
using UnityEngine;

[ExecuteAlways]
public class OutLine : MonoBehaviour
{
    private TextMeshProUGUI _tmpText;
    public Color _outlineColor = Color.white;

    public enum OutlineStyle
    {
        [InspectorName("1px (Underlay)")] px1,
        [InspectorName("2px (Underlay)")] px2,
        [InspectorName("4px (Underlay)")] px4,
    }

    [Header("外框樣式選擇")]
    public OutlineStyle _currentStyle;

    void Start()
    {
        UpdateColor();
    }

    void UpdateColor()
    {
        if (_tmpText == null) _tmpText = GetComponent<TextMeshProUGUI>();

        if (_tmpText != null && _tmpText.fontSharedMaterial != null)
        {
            Material mat = _tmpText.fontMaterial;

            float underlayDilate = 0f;
            float underlaySoftness = 0.05f;

            switch (_currentStyle)
            {
                case OutlineStyle.px1:
                default:
                    underlayDilate = 1f;
                    break;
                case OutlineStyle.px2:
                    underlayDilate = 2f;
                    break;
                case OutlineStyle.px4:
                    underlayDilate = 4f;
                    break;
            }

            mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0);

            mat.SetColor(ShaderUtilities.ID_UnderlayColor, _outlineColor);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, 0f);
            mat.SetFloat(ShaderUtilities.ID_UnderlayDilate, underlayDilate);
            mat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, underlaySoftness);

            mat.EnableKeyword("UNDERLAY_ON");
            mat.DisableKeyword("OUTLINE_ON");

            // 設定 Sharpness 增加銳利度（解決 Web 模糊）
            mat.SetFloat("_Sharpness", 0.2f);

            // 觸發 TMP 更新
            TMPro_EventManager.ON_DRAG_AND_DROP_MATERIAL_CHANGED(_tmpText.gameObject, _tmpText.fontMaterial, mat);

            _tmpText.ForceMeshUpdate();
            _tmpText.SetVerticesDirty();
            _tmpText.SetLayoutDirty();
        }
    }

    public void ChangeColor(string hexColor = "#ffffff")
    {
        Color newColor;
        if (ColorUtility.TryParseHtmlString(hexColor, out newColor))
        {
            _outlineColor = newColor;
            UpdateColor();
        }
        else
        {
            Debug.LogError("顏色格式錯誤！");
        }
    }

    private void OnValidate()
    {
        UpdateColor();
    }
}