using Newtonsoft.Json;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System;
using UnityEngine;
using System.ComponentModel;



[System.Serializable]
public class ShotData
{
    public string UID;
    public string playerUID;
    public Vector3 position;
    public Vector3 rotation;
}


[System.Serializable]
public class PositionUpdateData
{
    public string UID;
    public Vector3 position;
    public float rotation;
    public Vector3 velocity;
}

[Serializable]
public class GameStatePlayer
{
    public string UID { get; set; }



    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore), DefaultValue(false)]
    public bool Alive { get; set; } = false;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore), DefaultValue(false)]
    public bool DCed { get; set; } = false;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string joinData { get; set; } = null;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string pjdData { get; set; } = null;

    [JsonIgnore]
    public float joinedAt { get; set; } = 0;

    [JsonIgnore]
    public float DcedAt { get; set; } = 0;

    [JsonIgnore]
    public float lastReceivedUpdate { get; set; } = 0;
}


[Serializable]
public enum SSOType
{
    [EnumMember(Value = "0")]
    HealthUpdate,
    [EnumMember(Value = "1")]
    PowerRemove,
    [EnumMember(Value = "2")]
    RocketLaunch,
    [EnumMember(Value = "3")]
    RocketRemove,
    [EnumMember(Value = "4")]
    MineSpawn,
    [EnumMember(Value = "5")]
    MineRemove,
    [EnumMember(Value = "6")]
    NewRocketTarget,
    [EnumMember(Value = "7")]
    BrickChange,
    [EnumMember(Value = "8")]
    EndedPowerEffect,
    [EnumMember(Value = "9")]
    BrickRemove,
    [EnumMember(Value = "10")]
    HexRemove,
    [EnumMember(Value = "11")]
    HealthTest

}

[Serializable]
public class StateSyncObject
{

    public string SSOID { get; set; } = null;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore), DefaultValue(SSOType.HealthUpdate)]
    public SSOType type { get; set; } = SSOType.HealthUpdate;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore), DefaultValue("")]
    public string SSOData { get; set; } = "";



    [JsonIgnore]
    public float addedAt { get; set; } = 0;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string UID { get; set; } = null;
}


[Serializable]
public class GameStateUpdate
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string winner { get; set; } = null;

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore), DefaultValue(0)]
    public int playersAlive { get; set; } = 0;

    public List<GameStatePlayer> playerStates { get; set; } = new List<GameStatePlayer>();

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore), DefaultValue(false)]
    public bool GameRunning { get; set; }
    public List<StateSyncObject> StateSyncObjects { get; set; } = new List<StateSyncObject>();

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string GameStartData { get; set; } = null;


}