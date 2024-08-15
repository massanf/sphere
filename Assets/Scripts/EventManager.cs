using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class ClickSphere : UnityEvent<int>{}

public class EventManager : SingletonMonoBehaviour<EventManager>
{
    //ボタンクリックなどに応じて発行されるイベントを定義
    public ClickSphere clickSphere = new ClickSphere();
    public UnityEvent OK = new UnityEvent();
    public UnityEvent Clear = new UnityEvent();
    public UnityEvent Generated = new UnityEvent();
}


