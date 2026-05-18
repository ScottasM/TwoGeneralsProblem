using JetBrains.Annotations;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Messages;
using SocketIOClient.Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;

using System.Net.Sockets;
using Unity.VisualScripting;
using Unity.WebRTC;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Windows;
using static LobbyListWrapper;

public enum ConnectState
{
    SuccessToServer,
    FailToServer,
    SuccessToPlayer,
    FailToPlayer,
    AllChannesLoaded
};

public class Connection
{
    public NetworkingHandler networkingHandler;
    public Lobby lobby;
    public bool host;
    public LobbyPlayer player;
    public string guid;

    public void SendMessage(string text,string label)
    {
        if (networkingHandler == null)
            return;
        networkingHandler.SendMessage(label, text);
    }
}

public class WebSocketHandler : MonoBehaviour
{
    public static WebSocketHandler Instance;
    public SocketIOUnity socket;

    [SerializeField] private bool RelayByDefault = true;
    [SerializeField] private bool AllowHostToJoin = false;

    public void Awake()
    {
        DontDestroyOnLoad(this.gameObject);   
        Instance = this; 
    }

    public List<Connection> Connections = new List<Connection>();



    private Lobby passedLobbyForHost = null;

    public void ConnectSocketForHost(Lobby passedLobby)
    {
        MultiplayerManager.isHost = true;
        passedLobbyForHost = passedLobby;
        string uri = "http://185.80.129.173:5000/?b64=1";
        socket = new SocketIOUnity(uri, new SocketIOOptions {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket

        });
        socket.JsonSerializer = new NewtonsoftJsonSerializer();

        socket.OnDisconnected += (sender, e) => {
            //Debug.LogError("Disconnected with : " + e);
            MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.FailToServer);
            GameHost.instance.CloseHost(true);
        };

        socket.OnError += (sender, e) => {
            Debug.LogError("Failed to connect socket : " + e);
            MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.FailToServer);
            GameHost.instance.CloseHost(true);
        };

        socket.On("AnswerCreated", (response) => AnswerGot(response, null));
        socket.On("PeerConnected", (response) => PeerConnected(response, null));
        socket.On("IceGot", (response) => IceGot(response, null));

        socket.OnConnected += (sender, e) => {
            //ebug.Log("Clonnected");

            passedLobby.alreadyEstablished = true;
            MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.SuccessToServer);

            socket.EmitAsync("MakeRoom", Config.ApplicationID + "/" + passedLobby.uniqueID + "/" + "1");
            //Debug.Log("MakeRoom sent");
        };
        socket.OnDisconnected += (sender, e) => { Debug.LogWarning("Disconnected : " + e); };
        socket.OnReconnectFailed += (sender, e) => { Debug.LogError("ReconnectFailed:" + e); };
        socket.OnReconnectError += (sender, e) => { Debug.LogError("ReconnectError:" + e); };
        socket.OnReconnectAttempt += (sender, e) => { Debug.LogWarning("ReconnectAttempt:" + e); };

        AsyncConnection(passedLobby.uniqueID, socket);

    }

    public Connection ConnectSocket(Lobby passedLobby)
    {
        MultiplayerManager.isHost = false;
        
        string uri = "http://185.80.129.173:5000/?b64=1";
        socket = new SocketIOUnity(uri, new SocketIOOptions {
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket
            
        });
        socket.JsonSerializer = new NewtonsoftJsonSerializer();


        Connection conn = new Connection();

        GameObject gm = new GameObject("NetworkingHandlerHolderInstance");
        NetworkingHandler nH = gm.AddComponent<NetworkingHandler>();

        conn.networkingHandler = nH;
        conn.lobby = passedLobby;
        conn.host = false;


        socket.OnDisconnected += (sender,e) => {
            Debug.LogError("Disconnected with : " + e);
            MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.FailToServer);
        };

        socket.OnError += (sender, e) => {
            Debug.LogError("Failed to connect socket : " + e);
            MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.FailToServer);
            Connections.Remove(conn);
            socket.Disconnect();
        };

        socket.On("IceGot", (response) => IceGot(response, conn));
        socket.On("OfferReceived", (response) => OfferReceived(response, conn));
        socket.On("HostDisconnected", (response) => HostDisconnected(response, conn));
        socket.On("HostNotFound", (response) => HostNotFound(response, conn));
        passedLobby.conn = conn;

        socket.OnConnected += (sender, e) => {
            //Debug.Log("Clonnected");

            passedLobby.alreadyEstablished = true;
            MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.SuccessToServer);
            

            socket.EmitAsync("MakeRoom", Config.ApplicationID + "/" + passedLobby.uniqueID + "/" + "0");
            //Debug.Log("MakeRoom sent");
        };
        socket.OnDisconnected += (sender, e) => { Debug.LogWarning("Disconnected : " + e); };
        socket.OnReconnectFailed += (sender, e) => { Debug.LogError("ReconnectFailed:" + e); };
        socket.OnReconnectError += (sender, e) => { Debug.LogError("ReconnectError:" + e); };
        socket.OnReconnectAttempt += (sender, e) => { Debug.LogWarning("ReconnectAttempt:" + e); };

        Connections.Add(conn);

        AsyncConnection(passedLobby.uniqueID,socket);

        return conn;
    }


    public void HostNotFound(object response,Connection conn)
    {
        if (MultiplayerManager.isHost)
            return;
        MultiplayerManager.ConnectionStateChange?.Invoke(ConnectState.FailToPlayer);
        Debug.LogError("Host not found for lobby or lobby doesnt exist in signaling server. (Ussually it's just because the lobby was removed after the last refresh of lobbies)");
    }

    public async void AsyncConnection(string lobby_id, SocketIOUnity socket)
    {
        await socket.ConnectAsync();
        /*try {
            
            //await socket.ConnectAsync();
        }
        catch (Exception e) {
            Debug.LogError("Failed to connect socket : " + e.Message);
            // Handle the exception here
        }*/
    }

    public void HostDisconnected(object response, Connection conn)
    {
        LobbyManager.OnDisconnection(message:"Host disconnected",conn: conn);
    }

    public void PeerConnected(object response, Connection conn)
    {


        //Debug.LogWarning("Received peer connected + " + response);

        UnityMainThreadDispatcher.Instance().Enqueue(() => {


            if (MultiplayerManager.MyLobby == null)
                return;
            if (MultiplayerManager.MyLobby.currentPlayers >= MultiplayerManager.MyLobby.maxPlayers)
                return;

            string responseStr = response.ToString();
            List<string> responses = JsonConvert.DeserializeObject<List<string>>(responseStr);
            string guid = responses[0];
            //Debug.LogWarning(guid);
            Connection connection = new Connection();

            GameObject gm = new GameObject("NetworkingHandlerHolderInstance");
            NetworkingHandler nH = gm.AddComponent<NetworkingHandler>();
            //Debug.Log(1);

            connection.networkingHandler = nH;
            connection.lobby = passedLobbyForHost;
            connection.host = true;
            connection.guid = guid;
            Connections.Add(connection);


            //Debug.LogWarning("Peer connected called");

            connection.networkingHandler.crcn(true, guid, new RTCSessionDescription(), connection);
        });
       
    }

    public void AnswerGot(object response, Connection conn)
    {
        if(conn != null || !MultiplayerManager.isHost) {
            Debug.LogError("Something went wrong, received answer on player not host");
            return;
        }
        //Debug.LogWarning("Answer got");
        string responseStr = response.ToString();
        List<string> responses = JsonConvert.DeserializeObject<List<string>>(responseStr);

        string[] parts = responses[0].Split(new string[] { ";/;/;/" }, StringSplitOptions.None);

        conn = Connections.Find(u => u.guid == parts[1]);
        RTCSessionDescription answer = JsonConvert.DeserializeObject<RTCSessionDescription>(parts[0]);

        UnityMainThreadDispatcher.Instance().Enqueue(() => { conn.networkingHandler.GotAnswer(answer); });
        
    }

    public void IceGot(object response, Connection conn)
    {
        //Debug.LogWarning("Ice got");



        string responseStr = response.ToString();
        List<string> responses = JsonConvert.DeserializeObject<List<string>>(responseStr);
        string[] parts = responses[0].Split(new string[] { ";;//" }, StringSplitOptions.None);

        SerializableRTCIceCandidate sIceCandidate = JsonUtility.FromJson<SerializableRTCIceCandidate>(parts[0]);

        RTCIceCandidateInit init = new RTCIceCandidateInit {
            candidate = sIceCandidate.Candidate,
            sdpMid = sIceCandidate.SdpMid,
            sdpMLineIndex = sIceCandidate.SdpMLineIndex
        };
        if (init.sdpMLineIndex == -1) init.sdpMLineIndex = null;


        if(conn == null) {
            conn = Connections.Find(u => u.guid == parts[1]);
        }

        RTCIceCandidate candidate = new RTCIceCandidate(init);

        UnityMainThreadDispatcher.Instance().Enqueue(() => { conn.networkingHandler.GotIce(candidate); });
        
    }

    public void OfferReceived(object response, Connection conn)
    {
        if (conn.host)
            return;
        //Debug.LogWarning("Offer received");
        string responseStr = response.ToString();
        List<string> responses = JsonConvert.DeserializeObject<List<string>>(responseStr);

        string[] ls = responses[0].Split(new string[] { ";;//" }, StringSplitOptions.None);

        RTCSessionDescription answer = JsonConvert.DeserializeObject<RTCSessionDescription>(ls[0]);
        conn.networkingHandler.crcn(false, ls[1], answer,conn);
    }

    public void SendAnswer(Connection conn,string answer,string peerGUID)
    {
        socket.EmitAsync("SendAnswer", Config.ApplicationID + ";/;/;/" + conn.lobby.uniqueID + ";/;/;/" + peerGUID + ";/;/;/" + answer);
    }

    public void SendIce(string peerGUID, RTCIceCandidate candidate, Connection conn)
    {

        SerializableRTCIceCandidate sCand = new SerializableRTCIceCandidate(candidate);
        socket.EmitAsync("SendICE", Config.ApplicationID + ";/;/;/" + conn.lobby.uniqueID+ ";/;/;/" + peerGUID + ";/;/;/" + JsonConvert.SerializeObject(sCand));
    }

    public void SendOffer(string peerGUID, RTCSessionDescription sdp, Connection conn)
    {
        socket.EmitAsync("SendOffer", Config.ApplicationID + ";/;/;/" + conn.lobby.uniqueID + ";/;/;/" + peerGUID + ";/;/;/" + JsonConvert.SerializeObject(sdp));
    }


    /*private void OnMessage(object sender, MessageEventArgs e)
    {
        Debug.Log("Message from server: " + e.Data);
        // Handle the received message here
    }*/

   

    public void DisconnectSocket(Connection conn)
    {
        //Debug.Log("Disconnecting socket");
        if(Connections.Contains(conn)) { Connections.Remove(conn); }
        
    }

    public void RelayMessage(Connection conn, string message, string label)
    {
        foreach (Connection sendConn in Connections) {
            if (sendConn != conn) {
                sendConn.SendMessage(message,label);
            }
        }
    }


    public static void CloseAllConnections(string message)
    {
        try {
            for (int i = Instance.Connections.Count - 1; i >= 0; i--) {
                LobbyManager.OnDisconnection(message: message, conn: Instance.Connections[i]);
            }
        }
        catch (Exception e) { Debug.LogError("Error while closing connections : " + e); }
    }

    void OnDestroy()
    {
        CloseAllConnections("Host left");
    }

    private void OnApplicationQuit()
    {
        CloseAllConnections("App quit");
    }

}
