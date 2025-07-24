// ScriptCategoryAndMemoManager.cs
using UnityEngine;
using System.Collections.Generic;
using UnityEditor;
using System.Linq;

[System.Serializable]
public class ScriptMetadata
{
    public string scriptPath; // 원본 스크립트의 경로
    public string category;
    public string memo; // 원본 스크립트 개별 메모
    public List<TodoItem> todos = new List<TodoItem>(); // 원본 스크립트 개별 To-Do 리스트

    public ScriptMetadata(string path)
    {
        scriptPath = path;
        category = "Uncategorized"; // 기본 카테고리
        memo = "";
    }
}

[CreateAssetMenu(fileName = "ScriptCategoryAndMemoManager", menuName = "ScriptableObjects/Script Category & Memo Manager")]
public class ScriptCategoryAndMemoManager : ScriptableObject
{
    public List<string> categories = new List<string> { "Uncategorized" }; // 모든 카테고리 이름
    public List<ScriptMetadata> scriptMetadataList = new List<ScriptMetadata>(); // 각 스크립트의 메타데이터

    private static ScriptCategoryAndMemoManager _instance;
    public static ScriptCategoryAndMemoManager Instance
    {
        get
        {
            if (_instance == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:ScriptCategoryAndMemoManager");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    _instance = AssetDatabase.LoadAssetAtPath<ScriptCategoryAndMemoManager>(path);
                }
                else
                {
                    _instance = CreateInstance<ScriptCategoryAndMemoManager>();
                    AssetDatabase.CreateAsset(_instance, "Assets/Editor/ScriptCategoryAndMemoManager.asset");
                    AssetDatabase.SaveAssets();
                    Debug.Log("새 ScriptCategoryAndMemoManager.asset 생성");
                }
            }
            return _instance;
        }
    }

    public void AddCategory(string newCategory)
    {
        if (!categories.Contains(newCategory) && !string.IsNullOrWhiteSpace(newCategory))
        {
            categories.Add(newCategory);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }

    public ScriptMetadata GetOrCreateScriptMetadata(string scriptPath)
    {
        var metadata = scriptMetadataList.FirstOrDefault(m => m.scriptPath == scriptPath);
        if (metadata == null)
        {
            metadata = new ScriptMetadata(scriptPath);
            scriptMetadataList.Add(metadata);
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
        return metadata;
    }

    public void SetDirtyAndSave()
    {
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }
}