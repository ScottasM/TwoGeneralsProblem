using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UILobbies : MonoBehaviour
{
    public static UILobbies instance;


    [SerializeField] private TextMeshProUGUI lobbyText;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_InputField nickname;
    [SerializeField] public Toggle toggle;
    public string Nickname => nickname.text;



    private Action<ConnectState> _handler;
    private int loadedParts = 0;
    List<Lobby> lobbies = new List<Lobby>();

    public void Awake()
    {
        instance = this;
    }

    


    public void ShowHideLobbyUI(bool show)
    {
        if (show)
        {
            group.alpha = 1;
            group.blocksRaycasts = true;
        }
        else
        {
            group.alpha = 0;
            group.blocksRaycasts = false;
        }
    }

    public void Referesh()
    {
        Task.Run(() => RefreshTask());
    }


    

    private async void RefreshTask()
    {
        lobbies = await MultiplayerManager.GetLobbies();

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (lobbies.Count > 0)
            {

                lobbyText.transform.parent.gameObject.SetActive(true);
                lobbyText.text = lobbies[lobbies.Count-1].uniqueID;
            }
            else lobbyText.transform.parent.gameObject.SetActive(false);
        });
        
    }

    private Coroutine timeoutCoroutine;

    public void ClickedJoin()
    {
        if (lobbies.Count < 1)
            return;

        Connection conn = MultiplayerManager.Connect(lobbies[lobbies.Count-1]);
        GamePlayer.instance.StartPlayer(conn);
        MultiplayerManager.AllChannelsLoaded += GamePlayer.instance.SendPlayerJoined;
        loadedParts = 0;

        _handler = (state) => joiningLobbyStateChange(state, conn);

        MultiplayerManager.ConnectionStateChange += _handler;

        timeoutCoroutine = StartCoroutine(TimeOut());
    }


    private bool creatingLobby = false;
    public void ClickedCreate()
    {
        Task.Run(() => CreateLobby()); 
    }

    private async void CreateLobby()
    {
        if (creatingLobby)
            return;
        creatingLobby = true;

        if (MultiplayerManager.MyLobby != null)
        {
            MultiplayerManager.DeleteOwnedLobby();
        }


        string lobbyData = "randomname" + "||" + "testinName" + "||" + "3" + "||" + 1 + "||" + "0" + "||" + "";
        Lobby createdLobby = await MultiplayerManager.CreateLobby(lobbyData);


        if (createdLobby == null)
        {
            Debug.LogError("Lobby creation failed");
            return;
        }

        createdLobby.owner = "testinName";
        createdLobby.maxPlayers = 3;
        createdLobby.locked = false ? 1 : 0;
        createdLobby.password = "";
        createdLobby.currentPlayers = 1;
        createdLobby.name = "randomname";

        // do something, or do not. There is no try


        creatingLobby = false;
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            UILobbies.instance.ShowHideLobbyUI(false);
            GameHost.instance.StartHost();
        });
    }




    private IEnumerator TimeOut()
    {
        yield return new WaitForSecondsRealtime(10f);
        if (loadedParts >= 2)
            yield break;
        joiningLobbyStateChange(ConnectState.FailToPlayer, null);
    }


    public void joiningLobbyStateChange(ConnectState state, Connection conn)
     {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            if (conn == null)
            {
                GamePlayer.instance.ClosePlayer(true);
                Debug.LogError("Connection timed out...");
                StartCoroutine(joinScreenHideDelay());
                MultiplayerManager.ConnectionStateChange -= _handler;


                return;
            }
            if (state == ConnectState.SuccessToServer)
            {
                //Debug.Log("connected to server");
                Debug.LogError("Establishing connection to host...");

            }
            else if (state == ConnectState.FailToServer)
            {
                Debug.LogError("Failed to connect to server.");
                StartCoroutine(joinScreenHideDelay());
                MultiplayerManager.ConnectionStateChange -= _handler;
                GamePlayer.instance.ClosePlayer(true);
            }
            else if (state == ConnectState.SuccessToPlayer)
            {
                //Debug.Log("called");
                loadedParts++;
            }
            else if (state == ConnectState.AllChannesLoaded)
            {
                loadedParts++;
                //Debug.Log("called1");
            }
            else if (state == ConnectState.FailToPlayer)
            {
                Debug.LogError("Failed to connect to host");
                StartCoroutine(joinScreenHideDelay());
                MultiplayerManager.ConnectionStateChange -= _handler;
                GamePlayer.instance.ClosePlayer(true);
            }
            if (loadedParts == 2)
            {
                StopCoroutine(timeoutCoroutine);
                StartCoroutine(joinScreenHideDelay(1f));

            }
        });

    }

    IEnumerator joinScreenHideDelay(float time = 2f)
    {

        yield return new WaitForSecondsRealtime(time);
        MultiplayerManager.AllChannelsLoaded -= GamePlayer.instance.SendPlayerJoined;
    }

}



