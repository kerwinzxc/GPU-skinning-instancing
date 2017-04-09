using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoneAnimation : ScriptableObject {
    public string animName = null;
    public BoneAnimationFrame[] frames = null;
    public float length = 0;/*seconds*/
    public int fps = 0;
    public string[] bonesHierarchyNames = null;
}
