using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System;
using Unity.WebRTC;
using UnityEditor;
using Newtonsoft.Json;

[System.Serializable]
public class LobbyListWrapper
{
    public List<LobbyData> lobbies;

    [System.Serializable]
    public class LobbyData
    {
        public string lobby_id;
        public string lobby_info;
    }
}
public class LobbyResponse
{
    string response;
}
public static class LobbyManager
{

    private static bool isFetching = false;
    private static List<Lobby> cachedLobbies = new List<Lobby>();

    // to be called when you want to get all the lobbies for you application
    public static async Task<List<Lobby>> GetLobbies()
    {
        if(isFetching)
            return cachedLobbies;
        isFetching = true;

        cachedLobbies = await GetLobbiesAsync();

        isFetching = false;
        return cachedLobbies;
        
    }
    private static async Task<List<Lobby>> GetLobbiesAsync()
    {
        var tcs = new TaskCompletionSource<UnityWebRequestResult>();

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            try
            {
                WWWForm form = new WWWForm();
                form.AddField("Action", 3);
                form.AddField("application_id", Config.ApplicationID);
                form.AddField("lobby_id", "");
                form.AddField("lobby_data", "");

                UnityWebRequest www = UnityWebRequest.Post(Config.APIip, form);
                www.certificateHandler = new CertificateWhore();

                GameHost.instance.StartCoroutine(SendRequest(www, tcs));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });

        UnityWebRequestResult result = await tcs.Task;

        if (result.IsError)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Error while getting lobbies : " + result.Error);
#endif
            return new List<Lobby>();
        }

        LobbyListWrapper lobbyListWrapper =
            JsonUtility.FromJson<LobbyListWrapper>("{\"lobbies\":" + result.Result + "}");

        List<Lobby> lobbyList = new List<Lobby>();

        foreach (var lobbyData in lobbyListWrapper.lobbies)
        {
            Lobby lb = new Lobby();
            lb.uniqueID = lobbyData.lobby_id;

            string[] data = lobbyData.lobby_info.Split(
                new string[] { "||" },
                System.StringSplitOptions.None
            );

            if (data.Length < 6)
                continue; // safer than returning null mid-loop

            lb.name = data[0];
            lb.owner = data[1];
            lb.maxPlayers = int.Parse(data[2]);
            lb.currentPlayers = int.Parse(data[3]);
            lb.locked = int.Parse(data[4]);
            lb.password = data[5];

            lobbyList.Add(lb);
        }

        return lobbyList;
    }


    // to be called when lobby data chaanges
    public static void UpdateLobby(string lobbyData)
    {
        WWWForm form = new WWWForm();

        form.AddField("Action", 1);
        form.AddField("application_id", Config.ApplicationID);
        form.AddField("lobby_id", MultiplayerManager.MyLobby.uniqueID);

        form.AddField("lobby_data", lobbyData);

        UnityWebRequest www = UnityWebRequest.Post(Config.APIip, form);
        www.certificateHandler = new CertificateWhore();

        www.SendWebRequest();
    }

    public static async Task<Lobby?> CreateLobby(string lobbyData)
    {
        if (MultiplayerManager.MyLobby != null)
            return null;

        MultiplayerManager.MyLobby = new Lobby(true);
        MultiplayerManager.LastLobby = MultiplayerManager.MyLobby;

        var tcs = new TaskCompletionSource<UnityWebRequestResult>();

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            try
            {
                WWWForm form = new WWWForm();
                form.AddField("Action", 0);
                form.AddField("application_id", Config.ApplicationID);
                form.AddField("lobby_data", lobbyData);

                UnityWebRequest www = UnityWebRequest.Post(Config.APIip, form);
                www.certificateHandler = new CertificateWhore();

                // Run coroutine on main thread
                GameHost.instance.StartCoroutine(SendRequest(www, tcs));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });

        UnityWebRequestResult result = await tcs.Task;

        if (result.IsError)
        {
            MultiplayerManager.MyLobby = null;
            return null;
        }
        Lobby lob = await UnityMainThreadDispatcher.Instance().EnqueueAsync(() => {
            JObject json = JObject.Parse(result.Result);
            string lobbyId = (string)json["lobby_id"];

            MultiplayerManager.MyLobby.uniqueID = lobbyId;

            WebSocketHandler.Instance.ConnectSocketForHost(MultiplayerManager.MyLobby);
            PingLobby();

            return MultiplayerManager.MyLobby;
        });

        return lob;
        
    }

    private static IEnumerator SendRequest(UnityWebRequest www, TaskCompletionSource<UnityWebRequestResult> tcs)
    {
        yield return www.SendWebRequest();

        try
        {

            var result = new UnityWebRequestResult(www.downloadHandler.text,false);
            tcs.SetResult(result);
        }
        catch (Exception e)
        {
            tcs.SetException(e);
        }
    }

    public static async void DeleteOwnedLobby()
    {
        if (MultiplayerManager.MyLobby == null)
            return;

        var tcs = new TaskCompletionSource<UnityWebRequestResult>();

        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            try
            {
                WWWForm form = new WWWForm();

                form.AddField("Action", 2);
                form.AddField("application_id", Config.ApplicationID);
                form.AddField("lobby_id", MultiplayerManager.MyLobby.uniqueID);
                form.AddField("lobby_data", "");

                UnityWebRequest www = UnityWebRequest.Post(Config.APIip, form);
                www.certificateHandler = new CertificateWhore();

                GameHost.instance.StartCoroutine(SendRequest(www, tcs));
            }
            catch (Exception e)
            {
                tcs.SetException(e);
            }
        });

        // wait for request to finish (optional but correct)
        var result = await tcs.Task;

        if (result.IsError)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Failed to delete lobby: " + result.Error);
#endif
        }

        // only clear AFTER request completes
        MultiplayerManager.MyLobby = null;

        //UILobbies.instance.DeletedOwnLobby();
    }

    private static void PingLobby()
    {
        MonoBehaviourSingleton.Instance.StartCoroutine(PingLobbyCoroutine());
    }

    public enum DisconnectionType
    {
        ConnectingFailure,
        Left,
        ConnectionError,
        HostLeft
    }

    public static void OnDisconnection(DisconnectionType type = DisconnectionType.Left,
        string message = "",
        Connection conn = null)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {


#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("Connection closed with message : " + message);
#endif

            if (conn == null) {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("Passed to OnDisconnection without Connection object");
#endif
                return;
            }


            foreach (var channel in conn.networkingHandler.sendChannels) {
                conn.networkingHandler.sendChannels[channel.Key].Close();
            }

            foreach (var channel in conn.networkingHandler.receiveChannels) {
                conn.networkingHandler.receiveChannels[channel.Key].Close();
            }

            if (conn.networkingHandler.peerConnection != null)
                conn.networkingHandler.peerConnection.Close();

            GameObject.Destroy(conn.networkingHandler.gameObject);


            WebSocketHandler.Instance.DisconnectSocket(conn);

            MultiplayerManager.ConnectionClosed?.Invoke(conn, message);

            if (!MultiplayerManager.isHost)
                GamePlayer.instance.ClosePlayer(true);
        });
    }


    private static IEnumerator PingLobbyCoroutine()
    {
        while (true) {
            
            yield return new WaitForSecondsRealtime(5f);
            if (MultiplayerManager.MyLobby == null)
                break;
            WWWForm form = new WWWForm();

            form.AddField("Action", 4);
            form.AddField("application_id", Config.ApplicationID);
            form.AddField("lobby_id", MultiplayerManager.MyLobby.uniqueID);

            form.AddField("lobby_data", "");

            UnityWebRequest www = UnityWebRequest.Post(Config.APIip, form);
            www.certificateHandler = new CertificateWhore();

            yield return www.SendWebRequest();

            if(www.responseCode == 580) { // lobby was not found in server
                WebSocketHandler.CloseAllConnections("Server error : host server not found");
                MultiplayerManager.MyLobby = null;
            }
        }
    }
}


public class MonoBehaviourSingleton : MonoBehaviour
{
    private static MonoBehaviourSingleton instance;

    public static MonoBehaviourSingleton Instance
    {
        get {
            if (instance == null) {
                GameObject go = new GameObject("MonoBehaviourSingleton");
                instance = go.AddComponent<MonoBehaviourSingleton>();
                DontDestroyOnLoad(go);
            }
            return instance;
        }
    }
}
