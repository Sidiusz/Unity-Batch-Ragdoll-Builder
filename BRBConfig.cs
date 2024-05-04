#if UNITY_EDITOR
using UnityEngine;

[CreateAssetMenu(fileName = "BRBConfig", menuName = "Batch Ragdoll Builder/BRB Config")]
public class BRBConfig : ScriptableObject
{
    public string pelvis;
    public string leftHips;
    public string leftKnee;
    public string leftFoot;
    public string rightHips;
    public string rightKnee;
    public string rightFoot;
    public string leftArm;
    public string leftElbow;
    public string rightArm;
    public string rightElbow;
    public string middleSpine;
    public string head;
}
#endif