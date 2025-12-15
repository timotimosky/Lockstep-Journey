using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

// --- 1. 核心数据结构 (必须是值类型/确定性) ---

// PlayerInput 结构体与 SyncServer.cs 中的定义保持一致，如果放在单独文件会更好。
public struct PlayerInput
{
    public Vector3 moveDirection;
}




// --- 2. 帧同步管理器 ---

public class FrameSyncManager : MonoBehaviour
{
    // --- 调试参数 ---
    [Header("调试参数")]
    [Tooltip("模拟服务器权威帧与客户端本地帧的延迟 (N_delay)")]
    public int ServerDelayFrames = 5;

    // --- 状态变量 ---
    public int CurrentLocalFrame { get; private set; } = 0;
    public GameState CurrentState { get; private set; } = new GameState();

    // 服务器实例 (客户端持有服务器句柄用于通信)
    private SyncServer server = new SyncServer();

    // 存储历史状态，用于回滚 (Key: 帧号, Value: 状态快照)
    // 修复索引问题：我们让 historyStates[0] 存储初始状态，方便索引。
    private Dictionary<int, GameState> historyStates = new Dictionary<int, GameState>();
    private Dictionary<int, PlayerInput> historyPlayerInputs = new Dictionary<int, PlayerInput>();



    //我们本地玩家的 ID
    public const int curPlayerID = 1;

    private void Start()
    {
        // 初始化 historyStates[0] 为游戏的初始状态
        historyStates[0] = CloneState(CurrentState);
    }

    private void FixedUpdate()
    {
        // 1. 接收权威输入 (模拟网络接收)
        SimulateReceiveAuthoritativeInput();

        // 2. 预测/执行下一帧
        ExecuteNextFrame();

        // 3. 调试输出
        if (CurrentLocalFrame % 60 == 0)
        {
            Debug.Log($"Frame {CurrentLocalFrame}: State = {CurrentState}");
        }
    }

    private void ExecuteNextFrame()
    {
        CurrentLocalFrame++;

        // --- 模拟输入发送 (当前帧) ---
        PlayerInput localInput = GetLocalPlayerInput();

        // 发送给服务器 (模拟网络发送)
        server.ReceiveInput(CurrentLocalFrame, curPlayerID, localInput);
        // 存储本地输入 (用于未来重演)
        historyPlayerInputs.Add(CurrentLocalFrame, localInput);


        // --- 预测执行 ---

        // 1. 克隆当前状态作为下一帧的起点
        GameState nextState = CloneState(CurrentState);

        // 2. 应用自己的本地输入（预测执行）
        nextState.ApplyInput(curPlayerID, localInput);



        // 3. 推进状态
        CurrentState = nextState;
        Debug.Log($"本地预测：Frame {CurrentLocalFrame}: State = {CurrentState}");
        // 4. 存储状态快照 (用于未来回滚)
        historyStates[CurrentLocalFrame] = CloneState(CurrentState);
        

    }

    // ----------------------------------------------------------------------
    // --- 模拟网络和回滚逻辑 ---
    // ----------------------------------------------------------------------

    // ********* 模拟客户端输入 *********
    private PlayerInput GetLocalPlayerInput()
    {
        // 简单的输入：每 100 帧让 Player A 持续向右移动
        if (CurrentLocalFrame %2 ==0)
        {
            return new PlayerInput { moveDirection = Vector3.right };
        }
        return new PlayerInput { moveDirection = Vector3.zero };
    }

    // ********* 模拟权威输入接收和回滚决策 *********
    private void SimulateReceiveAuthoritativeInput()
    {
        // 期待收到的权威帧号 F_Expect
        int authFrameToReceive = CurrentLocalFrame - ServerDelayFrames;

        if (authFrameToReceive < 1) return; // 帧号从 1 开始

        // 从模拟服务器获取权威输入
        Dictionary<int, PlayerInput> authInputs = server.GetAuthoritativeInputs(authFrameToReceive);

        // 如果权威输入已到达，则进行回滚/重演
        if (authInputs != null)
        {
            // 在真正的帧同步中，这里要先对比预测是否正确，如果不正确才 Rollback
            // 在 Demo 中，我们直接执行 RollbackAndResimulate 来展示流程
            RollbackAndResimulate(authFrameToReceive, authInputs);
        }
    }

    /// <summary>
    /// 核心回滚逻辑：从分歧点回退，使用权威输入重演到当前帧
    /// </summary>
    private void RollbackAndResimulate(int rollbackFrame, Dictionary<int, PlayerInput> authInputs)
    {
        Debug.LogError($"Rollback! 从权威帧 {rollbackFrame} 开始重演，直到 {CurrentLocalFrame}。");

        // 1. 回退到分歧点的前一帧 (权威状态的起点)
        // 修复的索引：rollbackFrame - 1 确保我们总是能取到起点
        if (!historyStates.TryGetValue(rollbackFrame - 1, out GameState rollbackState))
        {
            // 理论上不应该发生，除非丢失了历史状态
            Debug.LogError($"无法从历史状态中找到起点帧 {rollbackFrame - 1}!");
            return;
        }

        // 2. 重演 Loop: 从分歧点开始，运行到当前帧
        GameState currentStateAtRollback = CloneState(rollbackState);

        for (int f = rollbackFrame; f <= CurrentLocalFrame; f++)
        {
            Dictionary<int, PlayerInput> inputsToApply = new Dictionary<int, PlayerInput>();

            // a) 如果是当前权威帧 (rollbackFrame)，使用服务器权威输入
            if (f == rollbackFrame)
            {

                inputsToApply = authInputs;
                //更新历史预测输入，以权威输入为准
                historyPlayerInputs[f] = inputsToApply[curPlayerID];
            }
            // b) 如果是未来帧 (> rollbackFrame)，使用客户端预测时存储的输入
            else
            {
                //从历史预测输入中取出用于重演
                inputsToApply[curPlayerID] = historyPlayerInputs[f];
            }

            // 应用输入，推进状态
            GameState nextState = CloneState(currentStateAtRollback);

            foreach (var kvp in inputsToApply)
            {
                nextState.ApplyInput(kvp.Key, kvp.Value);
            }

            Debug.LogError($"回滚,执行帧：Frame {f}: State = {nextState}");
            // 3. 更新历史状态 (用重演后的权威状态覆盖旧的预测状态)
            // 这确保了 historyStates 从此刻起是权威的
            historyStates[f] = CloneState(nextState);
            currentStateAtRollback = nextState;
        }

        // 4. 将最终重演的结果作为当前的权威状态
        Debug.LogError($"回滚,最终重演：Frame {CurrentLocalFrame}: State = {currentStateAtRollback}");
        CurrentState = currentStateAtRollback;
    }

    // ********* 辅助函数：深度克隆状态 *********
    private GameState CloneState(GameState original)
    {
        // 深度克隆是关键！确保回滚时不会修改历史状态。
        GameState clone = new GameState
        {
            Players[1] = original.PlayerAPosition,
            PlayerBPosition = original.PlayerBPosition
        };
        return clone;
    }
}