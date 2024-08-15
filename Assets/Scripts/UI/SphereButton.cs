using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SphereButton : MonoBehaviour
{
    [SerializeField]
    private int buttonIndex;
    public void OnClick(){
        EventManager.Instance.clickSphere.Invoke(buttonIndex);
    }
}
