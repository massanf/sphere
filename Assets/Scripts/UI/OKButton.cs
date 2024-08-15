using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OKButton : MonoBehaviour
{
    public void OnClick()
    {
        EventManager.Instance.OK.Invoke();
    }
}
