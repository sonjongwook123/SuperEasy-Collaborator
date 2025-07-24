// PartialScriptManager.cs
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq; // For LINQ operations

[System.Serializable]
public class TodoItem
{
    public string description;
    public bool isCompleted;

    public TodoItem(string desc)
    {
        description = desc;
        isCompleted = false;
    }
}

[System.Serializable]
public class PartialScriptInfo
{
    public string partialFilePath;      // partial 스크립트의 현재 경로
    public string originalScriptPath;   // 원본 스크립트의 경로
    public string featureName;          // 담당 기능 이름 (새 필드)
    public string authorName;
    public string creationDate;
    public string memo;                 // partial 스크립트 개별 메모
    public List<TodoItem> todos = new List<TodoItem>(); // partial 스크립트 개별 To-Do 리스트

    // 생성자에 featureName 추가
    public PartialScriptInfo(string partialPath, string originalPath, string feature, string author, string date)
    {
        partialFilePath = partialPath;
        originalScriptPath = originalPath;
        featureName = feature;
        authorName = author;
        creationDate = date;
        memo = "";
    }
}

[CreateAssetMenu(fileName = "PartialScriptManager", menuName = "ScriptableObjects/Partial Script Manager")]
public class PartialScriptManager : ScriptableObject
{
    public List<PartialScriptInfo> partialScripts = new List<PartialScriptInfo>();

    private static PartialScriptManager _instance;
    public static PartialScriptManager Instance
    {
        get
        {
            if (_instance == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:PartialScriptManager");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<PartialScriptManager>(path);
                }
                else
                {
                    _instance = CreateInstance<PartialScriptManager>();
                    AssetDatabase.CreateAsset(_instance, "Assets/Editor/PartialScriptManager.asset"); // 적절한 경로 설정
                    AssetDatabase.SaveAssets();
                    Debug.Log("새 PartialScriptManager.asset 생성");
                }
            }
            return _instance;
        }
    }

    // featureName 파라미터 추가
    public void AddPartialScript(string partialPath, string originalPath, string feature, string author, string date)
    {
        // 중복 추가 방지
        if (!partialScripts.Any(info => info.partialFilePath == partialPath))
        {
            PartialScriptInfo newInfo = new PartialScriptInfo(partialPath, originalPath, feature, author, date);
            partialScripts.Add(newInfo);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public void RemovePartialScript(string partialPath)
    {
        int removedCount = partialScripts.RemoveAll(info => info.partialFilePath == partialPath);
        if (removedCount > 0)
        {
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    // 파일 경로가 변경될 경우 업데이트
    public void UpdatePartialScriptPath(string oldPath, string newPath)
    {
        var info = partialScripts.FirstOrDefault(i => i.partialFilePath == oldPath);
        if (info != null)
        {
            info.partialFilePath = newPath;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    // SetDirtyAndSave 메서드 추가
    public void SetDirtyAndSave()
    {
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
}