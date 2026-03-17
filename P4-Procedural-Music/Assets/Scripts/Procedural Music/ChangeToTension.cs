using ProceduralMusic.Bridge;
using ProceduralMusic.Core;
using Unity.VisualScripting;
using UnityEngine;

public class ChangeToTension : MonoBehaviour
{
    public ProceduralMusicController proceduralMusicController;
    public void OnButtonClicked()
    {
        proceduralMusicController.SetKey(PitchClass.D);
        proceduralMusicController.SetGameState(GameMusicState.Tension);
        proceduralMusicController.SetTension(0.8f);
    }
}
