using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BoneAnimationFrame {
    public Bone[] bones = null;
    public Matrix4x4[] matrices = null;
}