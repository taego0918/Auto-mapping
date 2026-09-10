using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SelectUI : MonoBehaviour
{
    public GameObject _content;
    public GameObject _selectGameBtnPrefab;
    public Button _enterBtn;
    [SerializeField] Proxy _proxy;
    void Start()
    {
        Events.onSelectGameChanged += onSelectGameChanged;
        _enterBtn.onClick.AddListener(onClickEnterBtn);
        List<string> gameNameList = new List<string>() { "Initial d Deja vu", "The Chainsmokers", "Haruhikage" };
        gameNameList.ForEach(item =>
        {
            GameObject obj = Instantiate(_selectGameBtnPrefab, _content.transform, false);
            obj.GetComponent<SelectGameBtn>().SetData(item);
        });
    }

    void onSelectGameChanged(string value)
    {
        _enterBtn.gameObject.SetActive(true);
        _proxy.DefaultBgmAddress = value;
    }

    void onClickEnterBtn()
    {
        SceneManager.LoadScene("GameScene");
    }
}
