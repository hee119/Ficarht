using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class RoomManager : NetworkBehaviour
{
    public static RoomManager Instance;

    private void Awake()
    {
        Instance = this;
    }

    public class Room
    {
        public string roomCode;
        public List<NetworkConnectionToClient> players = new List<NetworkConnectionToClient>();
    }

    private Dictionary<string, Room> rooms = new Dictionary<string, Room>();

    // 🔥 방 생성
    [Server]
    public string CreateRoom(NetworkConnectionToClient conn)
    {
        string code = GenerateCode();

        Room room = new Room();
        room.roomCode = code;
        room.players.Add(conn);

        rooms.Add(code, room);

        Debug.Log($"방 생성: {code}");

        return code;
    }

    // 🔥 방 입장
    [Server]
    public void JoinRoom(string code, NetworkConnectionToClient conn)
    {
        if (!rooms.ContainsKey(code))
        {
            TargetJoinFailed(conn);
            return;
        }

        Room room = rooms[code];

        if (room.players.Count >= 2)
        {
            TargetJoinFailed(conn);
            return;
        }

        room.players.Add(conn);

        Debug.Log($"방 입장 성공: {code}");

        if (room.players.Count == 2)
        {
            StartGame(room);
        }
    }

    // 🔥 게임 시작 (맵 랜덤)
    [Server]
    void StartGame(Room room)
    {
        string[] maps =
        {
            "BattleScene_01",
            "BattleScene_02",
            "BattleScene_03"
        };

        string selectedMap = maps[Random.Range(0, maps.Length)];

        Debug.Log($"맵 선택: {selectedMap}");

        NetworkManager.singleton.ServerChangeScene(selectedMap);
    }

    [TargetRpc]
    void TargetJoinFailed(NetworkConnection target)
    {
        Debug.Log("방 입장 실패");
    }

    // 🔥 코드 생성
    string GenerateCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string result = "";

        for (int i = 0; i < 6; i++)
        {
            result += chars[Random.Range(0, chars.Length)];
        }

        return result;
    }
}
