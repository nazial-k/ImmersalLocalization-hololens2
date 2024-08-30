using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class SetParent : MonoBehaviourPunCallbacks
{

    public int PhotonParent=55;
    // Start is called before the first frame update
    void Start()
    {
        Transform parent = PhotonView.Find(PhotonParent).transform;
        gameObject.transform.SetParent(parent);
    }
}
