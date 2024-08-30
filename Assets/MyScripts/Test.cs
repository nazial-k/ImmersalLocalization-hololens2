using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Test : MonoBehaviour
{
    public PhotonView View;
    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    [PunRPC]
    private void test01 (){
        Debug.Log("Message Recevied");
    }
}