using Engine.Graphics;

namespace Game.Managers;

public static class VrManager
{
    public static bool IsVrAvailable => false;

    public static bool IsVrStarted => false;

    public static RenderTarget2D? VrRenderTarget => null;

    public static Matrix HmdMatrix => default;

    public static Matrix HmdMatrixInverted => default;

    public static Vector3 HmdMatrixYpr => default;

    public static Matrix HmdLastMatrix => default;

    public static Matrix HmdLastMatrixInverted => default;

    public static Vector3 HmdLastMatrixYpr => default;

    public static Vector2 HeadMove => default;

    public static Vector2 WalkingVelocity => default;

    public static void Initialize()
    {
    }

    public static void StartVr()
    {
    }

    public static void StopVr()
    {
    }

    public static void WaitGetPoses()
    {
    }

    public static void SubmitEyeTexture(VrEye eye, Texture2D texture)
    {
    }

    public static Matrix GetEyeToHeadTransform(VrEye eye)
    {
        return default;
    }

    public static Matrix GetProjectionMatrix(VrEye eye, float near, float far)
    {
        return default;
    }

    public static bool IsControllerPresent(VrController controller)
    {
        return false;
    }

    public static Matrix GetControllerMatrix(VrController controller)
    {
        return default;
    }

    public static Vector2 GetStickPosition(VrController controller, float deadZone = 0f)
    {
        return default;
    }

    public static Vector2? GetTouchpadPosition(VrController controller, float deadZone = 0f)
    {
        return default(Vector2);
    }

    public static float GetTriggerPosition(VrController controller, float deadZone = 0f)
    {
        return 0f;
    }

    public static bool IsButtonDown(VrController controller, VrControllerButton button)
    {
        return false;
    }

    public static bool IsButtonDownOnce(VrController controller, VrControllerButton button)
    {
        return false;
    }

    public static TouchInput? GetTouchInput(VrController controller)
    {
        return null;
    }
}
