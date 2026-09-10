using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SelectGameBtn : MonoBehaviour
{
    public Button _btn;
    public TextMeshProUGUI _btnText;
    string _gameName;

    void Start()
    {
        _btn.onClick.AddListener(onClickBtn);
        Events.onSelectGameChanged += onSelectGameChanged;
    }

    public void SetData(string gameName)
    {
        _gameName = gameName;
        _btnText.text = gameName;
    }
    public void onSelectGameChanged(string value)
    {
        Image image = gameObject.GetComponent<Image>();
        if (_gameName == value)
        {
            image.color = new Color32(174, 167, 231, 255);
        }
        else
        {
            image.color = new Color32(255, 255, 255, 255);
        }
    }

    void onClickBtn()
    {
        Events.onSelectGameChanged?.Invoke(_gameName);
    }

}
