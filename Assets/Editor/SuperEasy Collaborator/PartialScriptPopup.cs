// PartialScriptPopup.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;

public class PartialScriptPopup : EditorWindow
{
    private MonoScript originalScript;
    private Vector2 newTodoScrollPos; 
    private Vector2 partialListScrollPos;

    private Dictionary<string, string> newTodoDescriptions = new Dictionary<string, string>();

    // Partial 추가를 위한 필드
    private string newPartialFeatureName = "";
    private string newPartialAuthorName = ""; 

    private int currentPagePartial = 0;
    private const int partialsPerPage = 3; 

    public static void ShowPartialListForScript(MonoScript script, string defaultAuthorName)
    {
        PartialScriptPopup window = GetWindow<PartialScriptPopup>($"Partial Scripts for {script.name}");
        window.originalScript = script;
        window.newPartialAuthorName = "";
        window.minSize = new Vector2(500, 400); 
        window.Show();
    }

    void OnGUI()
    {
        if (originalScript == null)
        {
            EditorGUILayout.LabelField("원본 스크립트가 선택되지 않았습니다.");
            return;
        }

        EditorGUILayout.LabelField($"'{originalScript.name}' 관련 Partial 스크립트 관리", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("Partial 스크립트 추가", EditorStyles.boldLabel);
        newPartialFeatureName = EditorGUILayout.TextField("담당 기능 이름:", newPartialFeatureName);
        newPartialAuthorName = EditorGUILayout.TextField("작성자 이름:", newPartialAuthorName);

        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("➕ 새 Partial 스크립트 추가", GUILayout.Height(30)))
        {
            if (string.IsNullOrWhiteSpace(newPartialFeatureName))
            {
                EditorUtility.DisplayDialog("경고", "담당 기능 이름을 입력해주세요.", "확인");
            }
            else if (string.IsNullOrWhiteSpace(newPartialAuthorName))
            {
                EditorUtility.DisplayDialog("경고", "작성자 이름을 입력해주세요.", "확인");
            }
            else
            {
                if (CollaborationScriptEditor.CreateNewPartialScript(originalScript, newPartialFeatureName, newPartialAuthorName))
                {
                    newPartialFeatureName = ""; 
                    CollaborationScriptEditor editorWindow = GetWindow<CollaborationScriptEditor>();
                    if (editorWindow != null)
                    {
                        editorWindow.LoadScriptData();
                    }
                    AssetDatabase.Refresh();
                    Repaint(); 
                }
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        // --------------------------------------------------------

        List<PartialScriptInfo> filteredPartials = PartialScriptManager.Instance.partialScripts
            .Where(p => p.originalScriptPath == AssetDatabase.GetAssetPath(originalScript))
            .ToList();

        if (filteredPartials.Count == 0)
        {
            EditorGUILayout.LabelField("관련 Partial 스크립트가 없습니다.");
        }
        else
        {
            int totalPages = Mathf.CeilToInt((float)filteredPartials.Count / partialsPerPage);
            currentPagePartial = Mathf.Clamp(currentPagePartial, 0, totalPages > 0 ? totalPages - 1 : 0);

            int startIndex = currentPagePartial * partialsPerPage;
            int endIndex = Mathf.Min(startIndex + partialsPerPage, filteredPartials.Count);

            partialListScrollPos = EditorGUILayout.BeginScrollView(partialListScrollPos);
            for (int i = startIndex; i < endIndex; i++)
            {
                PartialScriptInfo info = filteredPartials[i];
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField($"파일: {Path.GetFileName(info.partialFilePath)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"기능명: {info.featureName}");
                EditorGUILayout.LabelField($"작성자: {info.authorName}");
                EditorGUILayout.LabelField($"작성일: {info.creationDate}");

                // Partial 스크립트 활성화/비활성화
                bool isActive = Path.GetExtension(info.partialFilePath) == ".cs";
                string buttonText = isActive ? "🔴 비활성화" : "🟢 활성화";
                GUI.backgroundColor = isActive ? Color.red : Color.green;
                if (GUILayout.Button(buttonText))
                {
                    TogglePartialScriptActiveState(info);
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.blue; 
                if (GUILayout.Button("스크립트 편집", GUILayout.Height(25)))
                {
                    MonoScript partialMonoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(info.partialFilePath);
                    if (partialMonoScript != null)
                    {
                        AssetDatabase.OpenAsset(partialMonoScript);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("오류", "Partial 스크립트 파일을 찾을 수 없습니다.", "확인");
                    }
                }
                GUI.backgroundColor = Color.white;
                // -------------------------------------------------
                EditorGUILayout.LabelField("메모:");
                string newMemo = EditorGUILayout.TextArea(info.memo, GUILayout.Height(40));
                if (newMemo != info.memo)
                {
                    info.memo = newMemo;
                    PartialScriptManager.Instance.SetDirtyAndSave();
                }
                
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button($"⬆️ '{Path.GetFileName(info.partialFilePath)}' 통합", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Partial 스크립트 통합", $"'{Path.GetFileName(info.partialFilePath)}' 내용을 '{originalScript.name}'에 통합하시겠습니까? 통합 후 Partial 스크립트 파일은 삭제됩니다.", "통합", "취소"))
                    {
                        if (CollaborationScriptEditor.IntegrateSelectedPartialScripts(originalScript, new List<PartialScriptInfo> { info }))
                        {
                            Repaint();
                            CollaborationScriptEditor editorWindow = GetWindow<CollaborationScriptEditor>();
                            if (editorWindow != null)
                            {
                                editorWindow.LoadScriptData(); 
                            }
                        }
                    }
                }
                GUI.backgroundColor = Color.white;

                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("삭제", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Partial 스크립트 삭제", $"'{Path.GetFileName(info.partialFilePath)}'를 정말로 삭제하시겠습니까?", "삭제", "취소"))
                    {
                        DeletePartialScriptFileAndInfo(info);
                    }
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (currentPagePartial > 0)
            {
                if (GUILayout.Button("이전 페이지"))
                {
                    currentPagePartial--;
                }
            }
            EditorGUILayout.LabelField($"{currentPagePartial + 1} / {totalPages}", GUILayout.Width(50), GUILayout.ExpandWidth(false));
            if (currentPagePartial < totalPages - 1)
            {
                if (GUILayout.Button("다음 페이지"))
                {
                    currentPagePartial++;
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("닫기", GUILayout.Height(30)))
        {
            Close();
        }
    }

    private void DrawTodoSection(List<TodoItem> todos, string uniqueIdForInput)
    {
    }
    
    private void TogglePartialScriptActiveState(PartialScriptInfo info)
    {
        string oldPath = info.partialFilePath;
        string newPath;

        if (Path.GetExtension(oldPath) == ".cs") 
        {
            newPath = Path.ChangeExtension(oldPath, ".disabled");
        }
        else 
        {
            newPath = Path.ChangeExtension(oldPath, ".cs");
        }

        string result = AssetDatabase.MoveAsset(oldPath, newPath);
        if (string.IsNullOrEmpty(result))
        {
            PartialScriptManager.Instance.UpdatePartialScriptPath(oldPath, newPath);
            Debug.Log($"Partial 스크립트 상태 변경: {oldPath} -> {newPath}");
            AssetDatabase.Refresh();
            Repaint();
            CollaborationScriptEditor window = GetWindow<CollaborationScriptEditor>();
            if (window != null)
            {
                window.LoadScriptData();
            }
        }
        else
        {
            EditorUtility.DisplayDialog("오류", $"Partial 스크립트 상태 변경 실패: {result}", "확인");
            Debug.LogError($"AssetDatabase.MoveAsset 오류: {result}");
        }
    }

    private void DeletePartialScriptFileAndInfo(PartialScriptInfo info)
    {
        if (File.Exists(info.partialFilePath))
        {
            AssetDatabase.DeleteAsset(info.partialFilePath);
        }
        PartialScriptManager.Instance.RemovePartialScript(info.partialFilePath);
        Debug.Log($"Partial 스크립트 '{Path.GetFileName(info.partialFilePath)}' 제거 완료.");
        AssetDatabase.Refresh();
        Repaint();
        CollaborationScriptEditor window = GetWindow<CollaborationScriptEditor>();
        if (window != null)
        {
            window.LoadScriptData();
        }
    }
}