using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InsectManager : MonoBehaviour {

    public GameObject dragonfly;
    public int amount = 5;

    void Start()
    {
        for(int i=0; i<amount; i++)
        {
            GameObject newD = Instantiate(dragonfly, transform);
        }
    }
}
