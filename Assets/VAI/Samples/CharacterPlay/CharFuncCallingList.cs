using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Threading.Tasks;

public class CharFuncCallingList : MonoBehaviour
{
    public List<GameObject> managedObjects = new List<GameObject>();
    private Dictionary<string, GameObject> objectLookup;

    [System.Serializable]
    public struct NamedClip
    {
        public string name;
        public AnimationClip clip;
    }

    public List<NamedClip> namedClips;

    // 只支持这些blendshape
    public static CharFuncCallingList Instance { get; private set; }

    private List<string> blendShapeNames = new List<string>{
        "mouthOpen",
        "mouthSmile",
        "browDownLeft",
        "browDownRight",
        "browInnerUp",
        "browOuterUpLeft",
        "browOuterUpRight",
        "eyeSquintLeft",
        "eyeSquintRight",
        "eyeWideLeft",
        "eyeWideRight",
        "jawForward",
        "jawLeft",
        "jawRight",
        "mouthFrownLeft",
        "mouthFrownRight",
        "mouthPucker",
        "mouthShrugLower",
        "mouthShrugUpper",
        "noseSneerLeft",
        "noseSneerRight",
        "mouthLowerDownLeft",
        "mouthLowerDownRight",
        "mouthLeft",
        "mouthRight",
        "eyeLookDownLeft",
        "eyeLookDownRight",
        "eyeLookUpLeft",
        "eyeLookUpRight",
        "eyeLookInLeft",
        "eyeLookInRight",
        "eyeLookOutLeft",
        "eyeLookOutRight",
        "cheekPuff",
        "cheekSquintLeft",
        "cheekSquintRight",
        "jawOpen",
        "mouthClose",
        "mouthFunnel",
        "mouthDimpleLeft",
        "mouthDimpleRight",
        "mouthStretchLeft",
        "mouthStretchRight",
        "mouthRollLower",
        "mouthRollUpper",
        "mouthPressLeft",
        "mouthPressRight",
        "mouthUpperUpLeft",
        "mouthUpperUpRight",
        "mouthSmileLeft",
        "mouthSmileRight",
        "tongueOut",
        "eyeBlinkLeft",
        "eyeBlinkRight"
    };

    public SkinnedMeshRenderer targetRenderer;
    public AnimationClip placeholderAnim;
    private Animator animator;


    void Awake()
    {
        // 初始化物体字典
        objectLookup = new Dictionary<string, GameObject>();
        foreach (var managedObject in managedObjects)
        {
            objectLookup[managedObject.gameObject.name.ToLower()] = managedObject.gameObject;
        }
        if (Instance == null)
        {
            Instance = this;
        }
        animator = targetRenderer.GetComponentInParent<Animator>();
        overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);
        animator.runtimeAnimatorController = overrideController;
    }

    private void OnEnable()
    {
    }
    private void OnDisable()
    {
        targetRenderer = null;
    }
    void OnDestroy()
    {
        Instance = null;
    }

    //测试
    // public string clipdeName;
    // public bool animationTrigger = false;
    // void Update()
    // {
    //     if (animationTrigger)
    //     {
    //         ReplaceCustomAnimMotion(clipdeName);
    //         animationTrigger = false;
    //     }
    //     else return;
    // }

    // 支持最多三个blendshape，至少一个，其他两个可以为空字符串。每个带独立weight。
    public string SetBlendShapes(
        string bSName1, float weight1,
        string bSName2 = null, float weight2 = 0f,
        string bSName3 = null, float weight3 = 0f)
    {
        if (targetRenderer == null)
        {
            Debug.LogError("Target SkinnedMeshRenderer is not assigned");
            return "Target SkinnedMeshRenderer is not assigned";
        }

        Mesh mesh = targetRenderer.sharedMesh;
        if (mesh == null)
        {
            Debug.LogError("SkinnedMeshRenderer.sharedMesh is null");
            return "SkinnedMeshRenderer.sharedMesh is null";
        }

        // Set all blendshapes to 0 first
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            targetRenderer.SetBlendShapeWeight(i, 0f);
        }

        // Helper function to set blendshape if name is not null or empty
        void SetBS(string name, float w)
        {
            if (!string.IsNullOrEmpty(name))
            {
                int idx = mesh.GetBlendShapeIndex(name);
                if (idx == -1)
                {
                    Debug.LogError($"Blendshape '{name}' not found on SkinnedMeshRenderer");
                }
                else
                {
                    targetRenderer.SetBlendShapeWeight(idx, w);
                }
            }
        }

        // Set up to three blendshapes
        SetBS(bSName1, weight1);
        SetBS(bSName2, weight2);
        SetBS(bSName3, weight3);
        return $"Done.";
    }

    public string ReplaceCustomAnimMotion(string clipName)
    {
        AnimationClip targetClip = null;
        foreach (var nc in namedClips)
        {
            if (nc.name == clipName)
            {
                targetClip = nc.clip;
                break;
            }
        }
        if (targetClip == null)
        {
            Debug.LogError($"AnimationClip: {clipName} not found");
            return $"AnimationClip: {clipName} not found";
        }

        PlayCustomAnimAndWait(targetClip);
        return $"Done.";
    }
    private AnimatorOverrideController overrideController;
    private bool isPlayingCustomAnim = false;
    private static readonly int EnterCustomAnimHash = Animator.StringToHash("EnterCustomAnim");
    private static readonly int ExitCustomAnimHash = Animator.StringToHash("ExitCustomAnim");

    private async Task PlayCustomAnimAndWait(AnimationClip customClip)
    {
        if (isPlayingCustomAnim)
        {
            Debug.LogWarning("Cannot play new animation, one is already in progress.");
            return;
        }
        if (placeholderAnim == null)
        {
            Debug.LogError("PlaceholderAnim is not assigned.");
            return;
        }

        const string stateToWaitFor = "CustomAnim";
        const int layerIndex = 0;

        try
        {
            isPlayingCustomAnim = true;
            // 1. Override the animation
            overrideController[placeholderAnim.name] = customClip;

            // 2. Trigger the state transition
            animator.SetTrigger(EnterCustomAnimHash);

            // 3. Wait until the animator has entered the target state
            await Task.Yield(); // Wait one frame for the transition to begin
            while (!animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateToWaitFor))
            {
                await Task.Yield();
            }

            // 4. Wait for the animation to complete
            while (animator.GetCurrentAnimatorStateInfo(layerIndex).normalizedTime < 1.0f)
            {
                // Ensure we are still in the same state, in case an interrupt occurs
                if (!animator.GetCurrentAnimatorStateInfo(layerIndex).IsName(stateToWaitFor)) break;
                await Task.Yield();
            }

            // 5. Trigger the exit transition from the state
            animator.SetTrigger(ExitCustomAnimHash);
        }
        finally
        {
            // 6. Restore the original animation and reset the flag
            // This 'finally' block ensures restoration even if the task is cancelled or an error occurs.
            isPlayingCustomAnim = false;
        }
        overrideController[placeholderAnim.name] = placeholderAnim;
    }

    public string SetExpressionHappy()
    {
        SetBlendShapes("mouthSmile", 1f, "eyeWideLeft", 0.5f, "eyeWideRight", 0.2f);
        return "";
    }

    public string SetExpressionSad()
    {
        SetBlendShapes("mouthFrownLeft", 1f, "mouthFrownRight", 0.3f, "eyeSquintRight", 0.3f);
        return "";
    }

    public string SetExpressionAngry()
    {
        SetBlendShapes("cheekPuff", 1f, "browDownLeft", 0.8f, "browDownRight", 0.8f);
        return "";
    }

    public string SetExpressionSurprised()
    {
        SetBlendShapes("mouthOpen", 1f, "eyeWideLeft", 0.4f, "eyeWideRight", 0.4f);
        return "";
    }

    public string SetExpressionDisgusted()
    {
        SetBlendShapes("mouthPucker", 1f, "eyeSquintLeft", 1f, "eyeSquintRight", 1f);
        return "";
    }

    public string ModifyTransform(string objectName, string transformType, float number)
    {
        if (!objectLookup.TryGetValue(objectName.ToLower(), out GameObject targetObject))
        { return $"Invalid: 无效的对象 '{objectName}'。"; }

        Transform camTransform = Camera.main.transform;
        const float animDuration = 0.5f;

        switch (transformType.ToLower())
        {
            case "moveright":
                StartCoroutine(MoveObject(targetObject.transform, camTransform.right * number, animDuration));
                return $"Ok,  '{objectName}' 已向右移动 {number} 米。";
            case "moveleft":
                StartCoroutine(MoveObject(targetObject.transform, -camTransform.right * number, animDuration));
                return $"Ok,  '{objectName}' 已向左移动 {number} 米。";
            case "moveup":
                StartCoroutine(MoveObject(targetObject.transform, camTransform.up * number, animDuration));
                return $"Ok,  '{objectName}' 已向上移动 {number} 米。";
            case "movedown":
                StartCoroutine(MoveObject(targetObject.transform, -camTransform.up * number, animDuration));
                return $"Ok,  '{objectName}' 已向下移动 {number} 米。";
            case "moveforward":
                if (objectName.ToLower() == "main camera")
                {
                    StartCoroutine(MoveCamera(camTransform.forward * number, animDuration));
                }
                else { StartCoroutine(MoveObject(targetObject.transform, -camTransform.forward * number, animDuration)); }
                return $"Ok,  '{objectName}' 已向前移动 {number} 米。";
            case "movebackward":
                if (objectName.ToLower() == "main camera")
                {
                    StartCoroutine(MoveCamera(-camTransform.forward * number, animDuration));
                }
                else { StartCoroutine(MoveObject(targetObject.transform, camTransform.forward * number, animDuration)); }
                return $"Ok,  '{objectName}' 已向后移动 {number} 米。";
            case "pitch": // 上下点头：围绕摄像机的 right 轴旋转
                StartCoroutine(AnimateRotation(targetObject.transform, camTransform.right, number, animDuration));
                return $"Ok,  '{objectName}' 已 Pitch 旋转 {number} 度。";
            case "yaw":   // 左右摇头：围绕摄像机的 up 轴旋转
                StartCoroutine(AnimateRotation(targetObject.transform, camTransform.up, number, animDuration));
                return $"Ok,  '{objectName}' 已 Yaw 旋转 {number} 度。";
            case "roll":  // 侧向翻滚：围绕摄像机的 forward 轴旋转
                StartCoroutine(AnimateRotation(targetObject.transform, camTransform.forward, number, animDuration));
                return $"Ok,  '{objectName}' 已 Roll 旋转 {number} 度。";
            case "scale":
                Vector3 targetScale = Vector3.one * number;
                StartCoroutine(AnimateScale(targetObject.transform, targetScale, animDuration));
                return $"Ok,  '{objectName}' 已改变大小至 {number}。";
            default:
                return $"Invalid: 无效的 transform 类型 '{transformType}'。";
        }
    }

    public string ChangeObjectColor(string objectName, string hexColor)
    {
        if (!objectLookup.TryGetValue(objectName.ToLower(), out GameObject targetObject))
        {
            return $"Invalid: 无效的对象 '{objectName}'。";
        }

        // ColorUtility.TryParseHtmlString 可以处理带或不带'#'的颜色代码
        ColorUtility.TryParseHtmlString(hexColor, out Color newColor);
        targetObject.GetComponent<Renderer>().material.color = newColor;
        return $"Ok,  '{objectName}' 的颜色已更改为 {hexColor}。";
    }

    public Light sceneLight; // 请确保在Inspector中赋值

    private Vector3 sunPathOrientation = new Vector3(0f, -30f, 0f);

    public string SetTimeOfDay(float timeOfDay)
    {
        // Clamp timeOfDay to [0, 24]
        timeOfDay = Mathf.Clamp(timeOfDay, 0f, 24f);

        StopAllCoroutines();

        StartCoroutine(AnimateSun(timeOfDay, 2.0f));

        // --- 光照强度逻辑 (此部分保持不变) ---
        float targetX = Mathf.Lerp(-20f, 200f, timeOfDay / 24f);
        float thisIntensity = sceneLight.intensity;
        if (targetX <= 180f && targetX >= 0f)
        {
            // 0到180，intensity 1
            StartCoroutine(LerpFloat(thisIntensity, 1f, 2f, t =>
            {
                sceneLight.intensity = t;
            }));
        }
        else
        {
            StartCoroutine(LerpFloat(thisIntensity, 0f, 2f, t =>
            {
                sceneLight.intensity = t;
            }));
        }

        return $"已将时间设置为 {timeOfDay:0.##} 点";
    }
    private IEnumerator MoveCamera(Vector3 direction, float animDuration)
    {
        Camera mainCamera = Camera.main;
        Vector3 startPos = mainCamera.transform.position;
        Vector3 endPos = startPos + direction;
        float elapsed = 0f;

        while (elapsed < animDuration)
        {
            float t = Mathf.Sin((elapsed / animDuration) * Mathf.PI * 0.5f);

            mainCamera.transform.position = Vector3.Lerp(
                startPos,
                endPos,
                t
            );
            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCamera.transform.position = endPos;
    }

    // 平滑移动物体的协程（使用正弦缓动函数）
    private IEnumerator MoveObject(Transform targetTransform, Vector3 direction, float duration)
    {
        Vector3 startPos = targetTransform.position;
        Vector3 endPos = startPos + direction;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // 使用正弦函数实现缓入缓出效果
            float t = Mathf.Sin((elapsed / duration) * Mathf.PI * 0.5f);

            targetTransform.position = Vector3.Lerp(
                startPos,
                endPos,
                t
            );
            elapsed += Time.deltaTime;
            yield return null;
        }

        targetTransform.position = endPos;
    }

    private IEnumerator LerpFloat(float startValue, float endValue, float duration, Action<float> onUpdate)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration); // 更平滑的缓入缓出 (Ease-In-Out)

            float currentValue = Mathf.Lerp(startValue, endValue, t);
            onUpdate?.Invoke(currentValue);

            elapsed += Time.deltaTime;
            yield return null;
        }
        onUpdate?.Invoke(endValue);
    }

    private IEnumerator AnimateRotation(Transform objectTransform, Vector3 axis, float angle, float duration)
    {
        Quaternion startRotation = objectTransform.rotation;
        // 计算相对于世界坐标系的最终旋转
        Quaternion endRotation = Quaternion.AngleAxis(angle, axis) * startRotation;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 使用世界坐标系插值
            objectTransform.rotation = Quaternion.Slerp(startRotation, endRotation, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        objectTransform.rotation = endRotation;
    }

    private IEnumerator AnimateScale(Transform targetTransform, Vector3 targetScale, float duration)
    {
        Vector3 startScale = targetTransform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            targetTransform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        targetTransform.localScale = targetScale;
    }
    private IEnumerator AnimateSun(float targetTimeOfDay, float duration)
    {
        float time = 0f;
        Quaternion startRotation = sceneLight.transform.rotation;
        float targetAngleX = Mathf.Lerp(-20f, 200f, targetTimeOfDay / 24f);

        Quaternion targetRotation = Quaternion.Euler(targetAngleX, sunPathOrientation.y, sunPathOrientation.z);

        while (time < duration)
        {
            float t = time / duration;
            t = t * t * (3f - 2f * t);

            // Quaternion.Slerp 的使用是正确的，保持不变
            sceneLight.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, t);

            time += Time.deltaTime;
            yield return null;
        }

        sceneLight.transform.rotation = targetRotation;
    }

}
