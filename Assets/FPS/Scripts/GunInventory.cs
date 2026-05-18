using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class GunInventory : MonoBehaviour {

	public GameObject currentGun;
	public List<string> gunsIHave = new List<string>();


	void Awake(){

		StartCoroutine ("SpawnWeaponUponStart");
	}

	IEnumerator SpawnWeaponUponStart(){
		yield return new WaitForSeconds (0.5f);
        GameObject resource = (GameObject)Resources.Load(gunsIHave[0].ToString());
        currentGun = (GameObject)Instantiate(resource, transform.position, gameObject.transform.rotation);

    }


	public void DeadMethod(){
		Destroy (currentGun);
		Destroy (this);
	}


}