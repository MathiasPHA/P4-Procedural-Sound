using ProceduralMusic.Bridge;
using UnityEngine;

public class JumpScareScript : MonoBehaviour
{
    bool hasBeenTriggered = false;
    public void JumpScare()
    {
        if (hasBeenTriggered == false)
        {
            GameStateManager.Instance.EnterHorror();
            hasBeenTriggered = true;
        }
        else
        {
            GameStateManager.Instance.ReturnToAuto();
            hasBeenTriggered = false;
        }
    }
}
