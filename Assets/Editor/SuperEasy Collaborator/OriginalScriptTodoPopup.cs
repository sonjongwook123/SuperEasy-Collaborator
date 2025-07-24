using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;

public class OriginalScriptTodoPopup : EditorWindow
{
    private MonoScript originalScript;
    private ScriptMetadata scriptMetadata;
    private Vector2 scrollPos;

    private Dictionary<string, string> newTodoDescriptions = new Dictionary<string, string>();

    public static void ShowWindow(MonoScript script, ScriptMetadata metadata)
    {
        OriginalScriptTodoPopup window = GetWindow<OriginalScriptTodoPopup>($"To-Do List for {script.name}");
        window.originalScript = script;
        window.scriptMetadata = metadata;
        window.minSize = new Vector2(300, 500);
        window.maxSize = new Vector2(400, 1000);
        window.Show();
    }

    void OnGUI()
    {
        if (originalScript == null || scriptMetadata == null)
        {
            EditorGUILayout.LabelField("원본 스크립트 또는 메타데이터가 선택되지 않았습니다.");
            return;
        }
        EditorGUILayout.Space();

        // To-Do 추가 섹션
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("새 To-Do 추가:", EditorStyles.boldLabel);
        string uniqueIdForInput = originalScript.name; 

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
                if (GUILayout.Button("삭제", GUILayout.Width(45)))
                {
                    scriptMetadata.todos.RemoveAt(i);
                    ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
                    Repaint(); // UI 업데이트
                    GUIUtility.ExitGUI(); 
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("닫기",  GUILayout.Height(30)))
        {
            Close();
        }
    }
}