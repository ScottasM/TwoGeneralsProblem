using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.Threading.Tasks;
using UnityEngine.Diagnostics;
using UnityEngine.UI;
using System.Net.Sockets;

public class MenuManager : MonoBehaviour
{
    /*
     * All this is for show, but can be perfectly used in your UI implementation, the main difference will probably be the DisplayLobbies function
     * If a user has a lobby created, he wont be able to join or create any new lobbies, so I would recommend putting the player inside some wait room or in the actual game
     */


    private Lobby selectedLobby;
    [SerializeField] private GameObject CreateScreen;
    [SerializeField] private GameObject LockX;
    [SerializeField] private TextMeshProUGUI maxPlayersText;
    [SerializeField] private GameObject lobbyMenuPrefab;
    [SerializeField] private GameObject lobbyHolder;
    [SerializeField] private GameObject lobbyScreen;



    private List<Lobby> lobbies = new List<Lobby>();
    private bool Locked = false;
    private string password;
    private int maxPlayers = 2;
    private string lobbyName;
    private bool isFetchingLobbies;

    public void Start()
    {
        GetLobbies();
    }

    public void CreateSettingsScreen(bool setActive)
    {
        CreateScreen.SetActive(setActive);
        lobbyScreen.SetActive(!setActive);
    }

    public void ChangeLockSetting()
    {
        Locked = !Locked;
        LockX.SetActive(Locked);
    }

    public void ChangePassword(string pass)
    {
        password = pass;
    }

    public void ChangeLobbyName(string name)
    {
        lobbyName = name;
    }

    public void ChangeMaxPlayers(bool right)
    {
        if (right)
            maxPlayers++;
        else if(!right && maxPlayers > 2)
            maxPlayers--;

        maxPlayersText.text = maxPlayers.ToString();
    }

    public async void CreateLobby()
    {

        string lobbyData = lobbyName + "||" + "testinName" + "||" + maxPlayers + "||" + 1 + "||" + Locked + "||" + password;
        Lobby createdLobby = await MultiplayerManager.CreateLobby(lobbyData);


        if(createdLobby == null) {
            Debug.LogError("Lobby creation failed");
            return;
        }

        createdLobby.owner = "testinName";
        createdLobby.maxPlayers = maxPlayers;
        createdLobby.locked = Locked ? 1 : 0;
        createdLobby.password = password;
        createdLobby.currentPlayers = 1;
        createdLobby.name = lobbyName;

        // do something, or do not. There is no try
    }

    
    // make sure that in your implementation you await for GetLobbies, if not it may result in delayed/inaccurate results
    public async void GetLobbies()
    {
        while (true) {
            if (!Application.isPlaying) {
                DestroyOldLobbies();
                break;
            }
            
            DestroyOldLobbies();
            lobbies = await MultiplayerManager.GetLobbies();
            DisplayLobbies(lobbies);
            await Task.Delay(8000);
        }
    }


    private void DestroyOldLobbies()
    {
        lobbies.Clear();
        Transform[] children = lobbyHolder.GetComponentsInChildren<Transform>();
        for (int j = children.Length-1; j >0; j--) {
            DestroyImmediate(children[j].gameObject);
        }

        
    }

    private void DisplayLobbies(List<Lobby> lobbies)
    {
        if (lobbies == null || lobbies.Count == 0)
            return;
        int i = 0;
        foreach (Lobby lob in lobbies) {
            GameObject obj = Instantiate(lobbyMenuPrefab);
            obj.transform.SetParent(lobbyHolder.transform);
            obj.transform.localScale = Vector3.one;
            obj.GetComponent<Button>().onClick.AddListener(() => ClickedJoin(lob));
            obj.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = lob.name;
            obj.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = lob.owner;
            obj.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = "Password : " + (lob.locked == 1 ? "yes" : "no");
            obj.transform.GetChild(3).GetComponent<TextMeshProUGUI>().text = "Players: " + lob.currentPlayers + "/" + lob.maxPlayers;
            i++;
        }
    }

    public void ClickedJoin(Lobby lob)
    {
        MultiplayerManager.Connect(lob);
    }

    public void OnApplicationQuit()
    {
        MultiplayerManager.DeleteOwnedLobby();
    }

}
