using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class Highlight : MonoBehaviour
{
    Image image;
    [SerializeField]
    private int imageIndex;
    private bool isEnabled;
    // Start is called before the first frame update
    void Start()
    {
        image=GetComponent<Image>();
        Disabled();
        EventManager.Instance.clickSphere.AddListener(ChangeState);
        EventManager.Instance.Generated.AddListener(Disabled);
        isEnabled=false;
    }

    void ChangeState(int index){
        if(index!=imageIndex){
            return;
        }
        if(isEnabled){
            image.color=new Color(0.0f,0.0f,0.0f);
            isEnabled=false;
        }else{
            image.color=new Color(1.0f,194.0f/255.0f,13.0f/255.0f);
            isEnabled=true;
        }
    }

    void Disabled(){
        image.color=new Color(0.0f,0.0f,0.0f);
        isEnabled=false;
    }
}
