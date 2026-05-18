using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.ComponentModel;
using Unity.VisualScripting.FullSerializer;
using System.Runtime.Serialization;
using System.Threading;
using System.Net.Sockets;
using static LobbyListWrapper;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;


public class LobbyPlayer
{
    public string name;
    public GameObject playerObject;
    public bool self = false;
    public int[] clothes;
    public string UID;
    public bool host;
    public bool Alive;
    public Connection? conn;
    public PositionUpdateData positionUpdateData;
    
    public MpPlayer mpPlayer;
}

[Serializable]
public class pooledJoinData
{
    public List<JoinData> joinDatas { get; set; } = new List<JoinData>();
}

[Serializable]
public class JoinData
{
    public string name;
    public int[] clothes = new int[11];
    public string UID;
    public bool host;
}


[System.Serializable]
public class PooledPositionData
{
    public List<PositionUpdateData> data;
}


public class GameHost : MonoBehaviour
{
    public static GameHost instance;

    public List<LobbyPlayer> lobbyPlayers = new List<LobbyPlayer>();

    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private Transform playerObject;

    private bool Alive = false;
    public bool GameRunning = false;

    private LobbyPlayer myPlayer;
    private bool changedPool = false;
    private int playersAlive;


    public const float DCRemoveCooldown = 5;
    public void Awake()
    {
        instance = this; 
    }


    void Start()
    {
        //DontDestroyOnLoad(gameObject);
        UnityMainThreadDispatcher.Instance();
    }

    private Action<Connection, string> _handler;

    public void StartHost()
    {
        MultiplayerManager.isHost = true;
        stateUpdater = new GameStateUpdate();
        processedSSO.Clear();
        PlayerBehaviour.instance.inMultiplayer = true;
        _handler = (Connection conn, string str) => PlayerJoined(conn,str,null);
        lobbyPlayers.Clear();
        MultiplayerManager.Channels["PlayerJoined"] += _handler;
        MultiplayerManager.Channels["UpdatePosition"] += ReceiveUpdatePosition;
        MultiplayerManager.Channels["PlayerDied"] += PlayerDied;
        MultiplayerManager.Channels["PlayerAction"] += PlayerActionUpdate;
        MultiplayerManager.Channels["StateSync"] += StateSyncReceive;
        MultiplayerManager.Channels["Shot"] += ShotReceive;
        MultiplayerManager.ConnectionClosed += PlayerLeft;
        //UIController.ShowPage(typeof(UIWaitRoom), true);
        PlaceSelf();
        //Camera.main.transform.parent.position = new Vector3(18.5f, 216.764f, 0f);
        //Camera.main.transform.parent.eulerAngles = new Vector3(0, 180, 0);
        //Camera.main.transform.localEulerAngles = new Vector3(20, 180, 0);
        StartCoroutine(GameStateUpdateSender());
        PlayerDefs.MovementEnabled = true;
        StartGame();
    }



    public void ShotReceive(Connection conn,string data)
    {
        ShotData shotdata = JsonConvert.DeserializeObject<ShotData>(data);
        conn.player.mpPlayer.ReceivedShot(shotdata);
    }


    public void PlayerJoined(Connection conn,string data,string additionalUID = null)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {



            if(additionalUID != null) {
                LobbyPlayer pl = lobbyPlayers.Find(u => u.UID == additionalUID);
                if(pl != null) {
                    return;
                }
            }
            JoinData joinData = JsonUtility.FromJson<JoinData>(data);

            MultiplayerManager.RelayMessage(conn, data, "PlayerJoined");

            pooledJoinData pjd = new pooledJoinData();
            foreach (var player in lobbyPlayers) {
                JoinData jd = new JoinData();
                jd.name = player.name;
                jd.clothes = player.clothes;
                jd.UID = player.UID;
                jd.host = player.host;
                //if (player.self) Debug.Log("Sending UID for join : " + jd.UID);

                pjd.joinDatas.Add(jd);
                conn.SendMessage(JsonUtility.ToJson(jd), "PlayerJoined");
            }
            



            /*for (int i = 0; i < 11; i++) {
                Debug.LogWarning("passed clothe " + joinData.clothes[i]);
            }*/
            LobbyPlayer lPlayer = CreatePlayer(joinData.name, joinData.clothes);

            lPlayer.self = false;
            lPlayer.UID = joinData.UID;
            lPlayer.host = false;
            lPlayer.conn = conn;
            conn.player = lPlayer;
            lPlayer.positionUpdateData = new PositionUpdateData();
            lPlayer.positionUpdateData.UID = joinData.UID;
            lobbyPlayers.Add(lPlayer);

            GameStatePlayer ps = new GameStatePlayer();
            ps.UID = joinData.UID;
            ps.joinData = data;
            ps.joinedAt = Time.realtimeSinceStartup;
            ps.pjdData = JsonConvert.SerializeObject(pjd);
            ps.lastReceivedUpdate = Time.realtimeSinceStartup + 2f;

            stateUpdater.playerStates.Add(ps);


            Lobby lob = MultiplayerManager.MyLobby;

            lob.currentPlayers = lobbyPlayers.Count;
            MultiplayerManager.UpdateLobby($"{lob.name}||{PlayerDefs.nickname}||{lob.maxPlayers}||{lob.currentPlayers}||{(lob.locked)}||{lob.password}");


        });

    }
    
    LobbyPlayer CreatePlayer(string name, int[] clothing)
    {
        GameObject pObject = Instantiate(playerPrefab);

        pObject.transform.localPosition = new Vector3(0, 0.8f, 0);

        LobbyPlayer lPlayer = new LobbyPlayer();
        lPlayer.mpPlayer = pObject.GetComponent<MpPlayer>();
        lPlayer.mpPlayer.Init();
        lPlayer.mpPlayer.InitNickname(name);
        lPlayer.name = name;
        lPlayer.clothes = clothing;
        lPlayer.playerObject = pObject;
        lPlayer.Alive = true;
        return lPlayer;
    }

    void PlaceSelf()
    {

        //int platformId = lobbyPlayers.Count;

        //playerObject.transform.localPosition = new Vector3(0, 0.2f, 0);

        LobbyPlayer lPlayer = new LobbyPlayer();
        lPlayer.playerObject = playerObject.gameObject;

        lPlayer.self = true;
        lPlayer.UID = Guid.NewGuid().ToString();
        lPlayer.host = true;
        lPlayer.conn = null;
        lPlayer.name = PlayerDefs.nickname;
        lobbyPlayers.Add(lPlayer);
        myPlayer = lPlayer;
        myPlayer.positionUpdateData = new PositionUpdateData();
        myPlayer.positionUpdateData.UID = myPlayer.UID;
    }

    public void PlayerLeft(Connection conn,string te)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            LobbyPlayer player = conn.player;
            if (player == null) {
                Debug.LogError("player that left doesnt exist");
                return;
            }


            if (player.Alive)
                PlayerDied(conn, "");




            MultiplayerManager.RelayMessage(conn, player.UID, "PlayerLeft");

            GameStatePlayer playerState = stateUpdater.playerStates.Find(u => u.UID == player.UID);

            if (playerState == null) {
                Debug.LogError("something went wrong, no player state on player leave");
            }
            else {
                playerState.DCed = true;
                playerState.DcedAt = Time.realtimeSinceStartup;
            }
            Destroy(player.playerObject);
            lobbyPlayers.Remove(player);
            Lobby lob = MultiplayerManager.MyLobby;
            lob.currentPlayers = lobbyPlayers.Count;
            MultiplayerManager.UpdateLobby($"{lob.name}||{PlayerDefs.nickname}||{lob.maxPlayers}||{lob.currentPlayers}||{(lob.locked)}||{lob.password}");
        });
    }

    

    public void CloseHost(bool stopRTC = false)
    {
        if (!MultiplayerManager.isHost) return;

        UnityMainThreadDispatcher.Instance().Enqueue(() => {

        


            //Debug.LogWarning("Host is closing");

            WebSocketHandler.Instance.socket.Disconnect();

            MultiplayerManager.Channels["PlayerJoined"] -= _handler;
            MultiplayerManager.Channels["UpdatePosition"] -= ReceiveUpdatePosition;
            MultiplayerManager.Channels["PlayerDied"] += PlayerDied;
            MultiplayerManager.Channels["PlayerAction"] -= PlayerActionUpdate;
            MultiplayerManager.Channels["StateSync"] -= StateSyncReceive;
            MultiplayerManager.Channels["Shot"] -= ShotReceive;
            MultiplayerManager.ConnectionClosed -= PlayerLeft;
            
        
            //UIController.ShowPage(typeof(UIMainMenu), true);


            PlayerBehaviour.instance.transform.SetParent(null);
            foreach (LobbyPlayer player in lobbyPlayers) {
                if (player.self) continue;
                if(player.playerObject != null)Destroy(player.playerObject);
            }
            lobbyPlayers.Clear();
            if (stopRTC) {
                WebSocketHandler.CloseAllConnections("");
            }
            MultiplayerManager.DeleteOwnedLobby();

            //@@
            //PlayerBehaviour.instance.inMultiplayer = false;
            //CameraBehavior.instance.Move = false;
            //PlayerBehaviour.instance.UpdateMovement = false;
            //CameraBehavior.instance.transform.parent.position = new Vector3(-184.6f, 0.2f, -3.72f);
            //CameraBehavior.instance.transform.eulerAngles = new Vector3(20, 0, 0);

            GameRunning = false;
            //UILobbies.instance.lobbies.Remove(MultiplayerManager.MyLobby);
            //UILobbies.instance.DisplayLobbies();
            
            //LevelController.DissposeLevel();
        
            MultiplayerManager.MyLobby = null;
            UILobbies.instance.ShowHideLobbyUI(true);
            PlayerBehaviour.instance.UpdateMovement = false;
            PlayerDefs.MovementEnabled = false;
            stateUpdater = null;
        });
    }

    public void StartGame()
    {
        Alive = true;
        GameRunning = false;

        playersAlive = lobbyPlayers.Count;
        PlayerBehaviour.instance.StartMpUpdate();
        StartCoroutine(sendPooledPosition());
    }

    IEnumerator sendPooledPosition()
    {
        PooledPositionData pooledData = new PooledPositionData();
        pooledData.data = new List<PositionUpdateData>();
        while (true) {
            yield return new WaitForSeconds(0.05f);
            if (!changedPool)
                continue;
            
            pooledData.data.Clear();
            foreach(LobbyPlayer player in lobbyPlayers) {
                pooledData.data.Add(player.positionUpdateData);
            }
            changedPool = false;
            MultiplayerManager.RelayMessage(null, JsonUtility.ToJson(pooledData), "UpdatePosition");

        }
    }

    [SerializeField] private float lerpSpeed;
    private Vector3 eulers = Vector3.zero; 
    public void ReceiveUpdatePosition(Connection conn,string data)
    {
        if (conn.player == null)
            return;
        
        if (conn.player != null && !conn.player.Alive) {
            Debug.LogWarning("Player is dead but still receiving pos data.");
            return;
        }
        PositionUpdateData positionUpdateData = JsonUtility.FromJson<PositionUpdateData>(data);
        conn.player.positionUpdateData = positionUpdateData;

        eulers.y = positionUpdateData.rotation;

        //conn.player.mpPlayer.playerRigidbody.linearVelocity = positionUpdateData.velocity;
        changedPool = true;
        conn.player.mpPlayer.ReceivedPosition(positionUpdateData.position, eulers, positionUpdateData.velocity);
    }

    public void SendSelfUpdate(PositionUpdateData data)
    {
        //Debug.Log("sending pos update");
        myPlayer.positionUpdateData.position = data.position;
        myPlayer.positionUpdateData.rotation = data.rotation;
        myPlayer.positionUpdateData.velocity = data.velocity;

        changedPool = true;
    }

    public void PlayerDied(Connection conn,string data)
    {

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            LobbyPlayer player;
            if (conn == null) {
                player = myPlayer;

                int aliveCount = 0; 

                foreach(LobbyPlayer lb in lobbyPlayers) {
                    if(lb.Alive && lb != myPlayer)
                        aliveCount++;
                }

                /*if (aliveCount > 1)
                    ActivateSpectate(true);*/

                //Debug.Log("self death");
                resetPlayerToWaiting();
            }
            else {
                player = conn.player;

                PlaceInStart(player);
                MultiplayerManager.RelayMessage(conn, player.UID, "PlayerDied");
            }

            GameStateUpdate state = new GameStateUpdate();

            player.Alive = false;
            playersAlive--;
            stateUpdater.playersAlive = playersAlive;

            state.playersAlive = playersAlive;
            state.winner = null;
            if (playersAlive <= 1) {


                //ActivateSpectate(false);
                GameRunning = false;

                stateUpdater.StateSyncObjects.Clear();

                
                
                foreach (LobbyPlayer pl in lobbyPlayers) {
                    
                    if (pl.Alive && state.winner == null) {
                        state.winner = pl.name;
                    }
                    if (!pl.self) {
                        PlaceInStart(pl);
                    }
                }
                if (state.winner == null) {
                    state.winner = "Error";
                }
                resetPlayerToWaiting();
            }

            stateUpdater.winner = state.winner;
            stateUpdater.playersAlive = state.playersAlive;
            //Debug.Log("sending game state update : " + JsonUtility.ToJson(state));
            MultiplayerManager.RelayMessage(null, JsonConvert.SerializeObject(state), "GameStateUpdate");
        });
    }


    private void resetPlayerToWaiting()
    {
        //playerObject.transform.localPosition = new Vector3(0, 0.2f, 0);

        playerObject.transform.GetChild(0).GetChild(0).eulerAngles = Vector3.zero;
        

        Alive = false;
        //playerObject.transform.GetChild(0).eulerAngles = Vector3.zero;
        //Camera.main.transform.parent.position = new Vector3(18.5f,216.764f,0f);
        //Camera.main.transform.parent.eulerAngles = new Vector3(0, 180, 0);
    }


    public void PlaceInStart(LobbyPlayer lb)
    {
        lb.Alive = false;
        
    }

    public void SendActionUpdate(StateSyncObject SSObject = null)
    {
        if (SSObject.UID == null)
            SSObject.UID = myPlayer.UID;

        
        SSObject.addedAt = Time.realtimeSinceStartup;
        SSObject.SSOID = Guid.NewGuid().ToString();

        stateUpdater.StateSyncObjects.Add(SSObject);
        MultiplayerManager.RelayMessage(null, JsonConvert.SerializeObject(SSObject), "PlayerAction");
    }


    public void PlayerActionUpdate(Connection conn, string data)
    {

        //Debug.LogWarning("Received action update:");
        //Debug.LogWarning(data);
        
        StateSyncObject dt = JsonConvert.DeserializeObject<StateSyncObject>(data);
        processSSO(conn,data,dt);
    }

    private void processSSO(Connection conn,string data = null, StateSyncObject dt = null)
    {
        if (dt == null)
            return;

        try {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {


                if (dt.SSOID != null) {

                    if (!processedSSO.ContainsKey(conn.player.UID)) {
                        processedSSO.Add(conn.player.UID, new List<string>());
                    }

                    if (processedSSO[conn.player.UID].Contains(dt.SSOID))
                        return;
                    processedSSO[conn.player.UID].Add(dt.SSOID);
                }

                switch (dt.type) {
                    case SSOType.HealthUpdate:
                        int health = int.Parse(dt.SSOData);
                        conn.player.mpPlayer.UpdateHealth(health);
                        break;
                }


            });
            if(data!= null)MultiplayerManager.RelayMessage(conn, data, "PlayerAction");
        }
        catch (Exception e) {
            Debug.LogError("Caught error in action update : " + e.Message);
        }
    }




    public GameStateUpdate stateUpdater = new GameStateUpdate();

    public const int StateSyncTimeout = 3;

    public IEnumerator GameStateUpdateSender()
    {

        List<GameStatePlayer> removeList = new List<GameStatePlayer> ();
        List<StateSyncObject> removeSSOList = new List<StateSyncObject>();

        while (true) {
            
            yield return new WaitForSeconds(0.5f);
            if (lobbyPlayers.Count == 0)
                continue;
            if (!PlayerBehaviour.instance.inMultiplayer)
                break;
            if (stateUpdater == null)
                break;
            removeList.Clear();
            removeSSOList.Clear();
            stateUpdater.GameRunning = GameRunning;
            foreach(GameStatePlayer state in stateUpdater.playerStates) {
                if (state.joinData != null) {
                    if (Time.realtimeSinceStartup > state.joinedAt + DCRemoveCooldown) {
                        state.joinData = null;
                        state.pjdData = null;
                    }
                }
                if (state.DCed) {
                    state.joinData = null;
                    state.pjdData = null;
                    if (Time.realtimeSinceStartup > state.DcedAt + DCRemoveCooldown) {
                        removeList.Add(state);
                        continue;
                    }
                }
                else {
                    foreach (LobbyPlayer player in lobbyPlayers) {
                        if (state.UID != player.UID) {
                            continue;
                        }
                        if (Time.realtimeSinceStartup >  state.lastReceivedUpdate + StateSyncTimeout) {
                            LobbyManager.OnDisconnection(message:"Player timed out",conn:player.conn);
                            continue;
                        }

                        state.Alive = player.Alive;
                    }
                }
                
            }

            foreach(StateSyncObject SSO in stateUpdater.StateSyncObjects) {
                if(Time.realtimeSinceStartup > SSO.addedAt + 3f) {
                    removeSSOList.Add(SSO);
                    continue;
                }
            }

            for(int i = 0; i < removeSSOList.Count; i++) {
                stateUpdater.StateSyncObjects.Remove(removeSSOList[i]);
            }

            for(int i = 0; i< removeList.Count; i++) {
                stateUpdater.playerStates.Remove(removeList[i]);
            }
            SendStateUpdate();
        }
    }


    private Dictionary<string, List<string>> processedSSO = new Dictionary<string, List<string>>();


    public void StateSyncReceive(Connection conn, string data)
    {
        //Debug.LogWarning("received state sync : " + data);

        UnityMainThreadDispatcher.Instance().Enqueue(() => {
            PlayerGameStateUpdate PGSupdate = JsonConvert.DeserializeObject<PlayerGameStateUpdate>(data);

            if (conn.player == null || !lobbyPlayers.Contains(conn.player)) {
                Debug.LogError("State sync received from player who doesnt exist or is not in players list");
                return;
            }

            string playerUID = conn.player.UID;

            GameStatePlayer st = stateUpdater.playerStates.Find(u => u.UID == playerUID);

            if (st == null) {
                Debug.LogError("Sometihing went wrong, #0052");
            }
            else st.lastReceivedUpdate = Time.realtimeSinceStartup;

            if (!processedSSO.ContainsKey(playerUID)) {
                processedSSO.Add(playerUID, new List<string>());
            }

            foreach (StateSyncObject SSObject in PGSupdate.StateSyncObjects) {

                if (stateUpdater.StateSyncObjects.Find(u => u.UID == SSObject.UID) == null)
                    continue;
                stateUpdater.StateSyncObjects.Add(SSObject);

                if (processedSSO[playerUID].Contains(SSObject.SSOID))
                    continue;
                processedSSO[playerUID].Add(SSObject.SSOID);
                processSSO(conn, dt: SSObject);

            }

            LobbyPlayer pl = lobbyPlayers.Find(u => u.UID == PGSupdate.UID);
            if (pl != null) {
                return;
            }

            if (pl.Alive == true && PGSupdate.Alive == false) {
                PlayerDied(conn, null);
            }

            if (PGSupdate.JoinData != null) {
                if (PGSupdate.UID != null) {
                    
                    PlayerJoined(conn, PGSupdate.JoinData,PGSupdate.UID);
                }
            }

            for (int i = processedSSO[playerUID].Count - 1; i >= 0; i--) {
                StateSyncObject sso = PGSupdate.StateSyncObjects.Find(u => u.SSOID == processedSSO[playerUID][i]);
                if (sso == null) {
                    processedSSO[playerUID].Remove(processedSSO[playerUID][i]);
                }
            }
        });
    }

    public void SendStateUpdate()
    {
        MultiplayerManager.RelayMessage(null, JsonConvert.SerializeObject(stateUpdater), "StateSync");
    }

    public void SendShot(ShotData data)
    {
        data.playerUID = myPlayer.UID;
        MultiplayerManager.RelayMessage(null, JsonUtility.ToJson(data), "Shot");
    }


}
