using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public static class MultiplayerManager
{
    // Events
    public static Action<Connection> ConnectionEstablished; // Gets called once connection is established
    public static Action<Connection, string> ConnectionClosed; // Gets called once connection closed/failed with added message of cause
    public static Action<Connection> AllChannelsLoaded;
    public static Action<ConnectState> ConnectionStateChange;

    /*
     * Sends a message to all other connections except the one you passed (do it only on host, since normal peers will have only one connection (to host))
     * This is usually done automatically one host receives a message. If you want to disable automatic relay and use this instead, disable set RelayByDefault on the WebSocketHandler object script
     */
    public static void RelayMessage(Connection conn, string message,string channelLabel) { WebSocketHandler.Instance.RelayMessage(conn, message,channelLabel); }

    /*
     * Done only on the peer connecting, dont call this on host.
     * This tries to establish a connection with the host, if the connection fails you will be notified with the events.
     */
    public static Connection Connect(Lobby lob) { return WebSocketHandler.Instance.ConnectSocket(lob); }
    
    // lobby methods
    public static async Task<List<Lobby>> GetLobbies() { return await LobbyManager.GetLobbies(); } // returns a list of lobbies from the database
    public static void UpdateLobby(string lobbyData) { LobbyManager.UpdateLobby(lobbyData); } // updates the created lobby
    public static async Task<Lobby> CreateLobby(string lobbyData) { return await LobbyManager.CreateLobby(lobbyData); } // Creates a lobby. Returns null if unsuccessfull, Lobby object if successful.
    public static async void DeleteOwnedLobby() { LobbyManager.DeleteOwnedLobby(); } // Deletes the owned lobby


    public static Dictionary<string, Action<Connection, string>> Channels = new Dictionary<string, Action<Connection, string>>() {
        {"UpdatePosition",UpdatePosition},
        {"PassLobbyData", PassLobbyData},
        {"PlayerJoined",PlayerJoined},
        {"PlayerLeft",PlayerLeft},
        {"StartGame",StartGame},
        {"PlayerDied",PlayerDied},
        {"GameStateUpdate",UpdateGameState},
        {"PlayerAction",PlayerAction},
        {"StateSync",StateSync },
        {"Shot",Shot }
    };

    public static Action<Connection, string> UpdatePosition;
    public static Action<Connection, string> PassLobbyData;
    public static Action<Connection, string> PlayerJoined;
    public static Action<Connection, string> PlayerLeft;
    public static Action<Connection, string> StartGame;
    public static Action<Connection, string> CountDown;
    public static Action<Connection, string> PlayerDied;
    public static Action<Connection, string> UpdateGameState;
    public static Action<Connection, string> PlayerAction;
    public static Action<Connection, string> StateSync;
    public static Action<Connection, string> Shot;


    public static bool isHost = false;

    public static Lobby MyLobby = null;
    public static Lobby LastLobby = null;




    

}
