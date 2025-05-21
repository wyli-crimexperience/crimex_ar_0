using UnityEngine;

public enum InteractionMode
{
    Transform,
    Description
}

public static class InteractionState
{
    public static InteractionMode CurrentMode = InteractionMode.Transform;
}