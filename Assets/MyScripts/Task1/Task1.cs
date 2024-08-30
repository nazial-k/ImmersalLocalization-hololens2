using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class Task1 : MonoBehaviour
{
    public GameObject MRSpherePrefab;

    public void TestClick(){
        Debug.Log("Pressing");
    }

    [PunRPC]
    public void spawnSpheres(){
        GameObject MRSphere = Instantiate(MRSpherePrefab);
    }

}