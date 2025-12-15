using System.Collections.Generic;
using UnityEngine;

public class PlayerState
{
    public Vector3 Position = Vector3.zero;
}


public class GameState
{

    public Dictionary<int, PlayerState> Players = new Dictionary<int, PlayerState>();

    public GameState()
    {
        // 初始化两个玩家
        Players[1] = new PlayerState();
        Players[2] = new PlayerState();
    }

    private const float Speed = 5.0f;

    public void ApplyInput(int playerID, PlayerInput input)
    {
        // 帧同步的理想步长应该是固定的 TIME_STEP，而非 Unity 的 fixedDeltaTime
        // 但为演示目的，我们沿用 fixedDeltaTime
        Vector3 movement = input.moveDirection.normalized * Speed * Time.fixedDeltaTime;

        Players[playerID].Position += movement;
    }

    public override string ToString()
    {
        return $"A Pos: {Players[1]:F2}, B Pos: {Players[2]:F2}";
    }
}