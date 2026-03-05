using UnityEngine;

public enum BindingAction
{
    None = 0,
    Forward = 1,
    Backward = 2,
    Left = 3,
    Right = 4,
    Run = 5,
    Jump = 6,
    Action = 7
}

public static class GameInputBindings
{
    private const string PrefPrefix = "InputBinding.";
    private static bool loaded;
    public static bool RunLocked { get; set; }

    public static KeyCode ForwardKey { get; private set; } = KeyCode.W;
    public static KeyCode BackwardKey { get; private set; } = KeyCode.S;
    public static KeyCode LeftKey { get; private set; } = KeyCode.A;
    public static KeyCode RightKey { get; private set; } = KeyCode.D;
    public static KeyCode RunKey { get; private set; } = KeyCode.LeftShift;
    public static KeyCode JumpKey { get; private set; } = KeyCode.Space;
    public static KeyCode ActionKey { get; private set; } = KeyCode.E;

    public static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        ForwardKey = Load("Forward", KeyCode.W);
        BackwardKey = Load("Backward", KeyCode.S);
        LeftKey = Load("Left", KeyCode.A);
        RightKey = Load("Right", KeyCode.D);
        RunKey = Load("Run", KeyCode.LeftShift);
        JumpKey = Load("Jump", KeyCode.Space);
        ActionKey = Load("Action", KeyCode.E);
        loaded = true;
    }

    public static KeyCode GetKey(BindingAction action)
    {
        EnsureLoaded();
        switch (action)
        {
            case BindingAction.Forward:
                return ForwardKey;
            case BindingAction.Backward:
                return BackwardKey;
            case BindingAction.Left:
                return LeftKey;
            case BindingAction.Right:
                return RightKey;
            case BindingAction.Run:
                return RunKey;
            case BindingAction.Jump:
                return JumpKey;
            case BindingAction.Action:
                return ActionKey;
            default:
                return KeyCode.None;
        }
    }

    public static void SetKey(BindingAction action, KeyCode key)
    {
        EnsureLoaded();
        switch (action)
        {
            case BindingAction.Forward:
                ForwardKey = key;
                Save("Forward", key);
                break;
            case BindingAction.Backward:
                BackwardKey = key;
                Save("Backward", key);
                break;
            case BindingAction.Left:
                LeftKey = key;
                Save("Left", key);
                break;
            case BindingAction.Right:
                RightKey = key;
                Save("Right", key);
                break;
            case BindingAction.Run:
                RunKey = key;
                Save("Run", key);
                break;
            case BindingAction.Jump:
                JumpKey = key;
                Save("Jump", key);
                break;
            case BindingAction.Action:
                ActionKey = key;
                Save("Action", key);
                break;
        }
    }

    private static KeyCode Load(string key, KeyCode defaultValue)
    {
        return (KeyCode)PlayerPrefs.GetInt(PrefPrefix + key, (int)defaultValue);
    }

    private static void Save(string key, KeyCode value)
    {
        PlayerPrefs.SetInt(PrefPrefix + key, (int)value);
        PlayerPrefs.Save();
    }
}
