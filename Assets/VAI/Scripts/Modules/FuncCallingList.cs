using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class FuncCallingList : MonoBehaviour
{
    public List<GameObject> managedObjects = new List<GameObject>();
    private Dictionary<string, GameObject> objectLookup;

    void Awake()
    {
        // 初始化物体字典
        objectLookup = new Dictionary<string, GameObject>();
        foreach (var managedObject in managedObjects)
        {
            objectLookup[managedObject.gameObject.name.ToLower()] = managedObject.gameObject;
        }
    }
    
    public string ModifyTransform(string objectName, string transformType, float number) 
    {
        if (!objectLookup.TryGetValue(objectName.ToLower(), out GameObject targetObject))
        { return $"错误: 无效的对象 '{objectName}'。"; }
        
        Transform camTransform = Camera.main.transform;
        const float animDuration = 0.5f;
        
        switch (transformType.ToLower())
        {
            case "moveright":
                StartCoroutine(MoveObject(targetObject.transform, camTransform.right * number, animDuration));
                return $"成功: 对象 '{objectName}' 已向右移动 {number} 米。";
            case "moveleft":
                StartCoroutine(MoveObject(targetObject.transform, -camTransform.right * number, animDuration));
                return $"成功: 对象 '{objectName}' 已向左移动 {number} 米。";
            case "moveup":
                StartCoroutine(MoveObject(targetObject.transform, camTransform.up * number, animDuration));
                return $"成功: 对象 '{objectName}' 已向上移动 {number} 米。";
            case "movedown":
                StartCoroutine(MoveObject(targetObject.transform, -camTransform.up * number, animDuration));
                return $"成功: 对象 '{objectName}' 已向下移动 {number} 米。";
            case "moveforward":
                StartCoroutine(MoveObject(targetObject.transform, -camTransform.forward * number, animDuration));
                return $"成功: 对象 '{objectName}' 已向前移动 {number} 米。";
            case "movebackward":
                StartCoroutine(MoveObject(targetObject.transform, camTransform.forward * number, animDuration));
                return $"成功: 对象 '{objectName}' 已向后移动 {number} 米。";
            case "pitch": // 上下点头：围绕摄像机的 right 轴旋转
                StartCoroutine(AnimateRotation(targetObject.transform, camTransform.right, number, animDuration));
                return $"成功: 对象 '{objectName}' 已 Pitch 旋转 {number} 度。";
            case "yaw":   // 左右摇头：围绕摄像机的 up 轴旋转
                StartCoroutine(AnimateRotation(targetObject.transform, camTransform.up, number, animDuration));
                return $"成功: 对象 '{objectName}' 已 Yaw 旋转 {number} 度。";
            case "roll":  // 侧向翻滚：围绕摄像机的 forward 轴旋转
                StartCoroutine(AnimateRotation(targetObject.transform, camTransform.forward, number, animDuration));
                return $"成功: 对象 '{objectName}' 已 Roll 旋转 {number} 度。";
            case "scale":
                Vector3 targetScale = Vector3.one * number;
                StartCoroutine(AnimateScale(targetObject.transform, targetScale, animDuration));
                return $"成功: 对象 '{objectName}' 已改变大小至 {number}。";
            default:
                return $"错误: 无效的 transform 类型 '{transformType}'。";
        }
}

    public string ChangeObjectColor(string objectName, string hexColor)
    {
        if (!objectLookup.TryGetValue(objectName.ToLower(), out GameObject targetObject))
        {
            return $"错误: 无效的对象 '{objectName}'。";
        }
        
        // ColorUtility.TryParseHtmlString 可以处理带或不带'#'的颜色代码
        ColorUtility.TryParseHtmlString(hexColor, out Color newColor);
        targetObject.GetComponent<Renderer>().material.color = newColor;
        return $"成功: 对象 '{objectName}' 的颜色已更改为 {hexColor}。";
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
}
