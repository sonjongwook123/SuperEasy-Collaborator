// OriginalScriptTodoPopup.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;

public class OriginalScriptTodoPopup : EditorWindow
{
    private MonoScript originalScript;
    private ScriptMetadata scriptMetadata; // 원본 스크립트의 메타데이터
    private Vector2 scrollPos;

    private Dictionary<string, string> newTodoDescriptions = new Dictionary<string, string>(); // To-Do 입력을 위한 임시 필드 (스크립트 경로 기반)

    public static void ShowWindow(MonoScript script, ScriptMetadata metadata)
    {
        OriginalScriptTodoPopup window = GetWindow<OriginalScriptTodoPopup>($"To-Do List for {script.name}");
        window.originalScript = script;
        window.scriptMetadata = metadata;
        window.minSize = new Vector2(400, 300); // 팝업 최소 크기 설정
        window.Show();
    }

    void OnGUI()
    {
        if (originalScript == null || scriptMetadata == null)
        {
            EditorGUILayout.LabelField("원본 스크립트 또는 메타데이터가 선택되지 않았습니다.");
            return;
        }

        EditorGUILayout.LabelField($"**'{originalScript.name}' To-Do 목록**", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // To-Do 추가 섹션
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("새 To-Do 추가:", EditorStyles.boldLabel);
        string uniqueIdForInput = originalScript.name; // 입력 필드를 위한 고유 ID

        if (!newTodoDescriptions.ContainsKey(uniqueIdForInput))
        {
            newTodoDescriptions[uniqueIdForInput] = "";
        }

        EditorGUILayout.BeginHorizontal();
        newTodoDescriptions[uniqueIdForInput] = EditorGUILayout.TextField("", newTodoDescriptions[uniqueIdForInput]);
        if (GUILayout.Button("➕ 추가", GUILayout.Width(60)))
        {
            string newTodoDesc = newTodoDescriptions[uniqueIdForInput];
            if (!string.IsNullOrWhiteSpace(newTodoDesc))
            {
                scriptMetadata.todos.Add(new TodoItem(newTodoDesc));
                ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
                newTodoDescriptions[uniqueIdForInput] = "";
                Repaint(); // UI 업데이트
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();


        // To-Do 목록 표시
        if (scriptMetadata.todos.Count == 0)
        {
            EditorGUILayout.HelpBox("등록된 To-Do 항목이 없습니다.", MessageType.Info);
        }
        else
        {
            // 진행률 바
            int completedTodos = scriptMetadata.todos.Count(t => t.isCompleted);
            int totalTodos = scriptMetadata.todos.Count;
            string todoStatus = totalTodos > 0 ? $"({completedTodos}/{totalTodos})" : "(0/0)";
            float progress = totalTodos > 0 ? (float)completedTodos / totalTodos : 0f;

            Rect progressBarRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            EditorGUI.ProgressBar(progressBarRect, progress, $"진행 상태: {todoStatus}");
            EditorGUILayout.Space(5);

            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            for (int i = 0; i < scriptMetadata.todos.Count; i++)
            {
                EditorGUILayout.BeginHorizontal(GUI.skin.box);
                bool newIsCompleted = EditorGUILayout.Toggle(scriptMetadata.todos[i].isCompleted, GUILayout.Width(20));
                if (newIsCompleted != scriptMetadata.todos[i].isCompleted)
                {
                    scriptMetadata.todos[i].isCompleted = newIsCompleted;
                    ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
                    Repaint(); // UI 업데이트
                }
                EditorGUILayout.LabelField(scriptMetadata.todos[i].description, scriptMetadata.todos[i].isCompleted ? EditorStyles.miniLabel : EditorStyles.label);
                if (GUILayout.Button("🗑️", GUILayout.Width(25)))
                {
                    scriptMetadata.todos.RemoveAt(i);
                    ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
                    Repaint(); // UI 업데이트
                    GUIUtility.ExitGUI(); // 삭제 후 즉시 GUI 종료하여 오류 방지
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("닫기", GUILayout.Height(30)))
        {
            Close();
        }
    }
}