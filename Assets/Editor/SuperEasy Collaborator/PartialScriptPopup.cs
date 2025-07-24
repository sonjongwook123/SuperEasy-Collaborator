using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System;

public class PartialScriptPopup : EditorWindow
{
    private MonoScript originalScript;
    private Vector2 scrollPos;
    private Dictionary<string, string> newTodoDescriptions = new Dictionary<string, string>();

    // Partial 추가를 위한 필드
    private string newPartialFeatureName = "";
    private string newPartialAuthorName = ""; // CollaborationScriptEditor에서 전달받은 기본값

    public static void ShowPartialListForScript(MonoScript script, string defaultAuthorName)
    {
        PartialScriptPopup window = GetWindow<PartialScriptPopup>($"Partial Scripts for {script.name}");
        window.originalScript = script;
        window.newPartialAuthorName = defaultAuthorName; // 기본 작성자 이름 설정
        window.minSize = new Vector2(500, 400); // 팝업 최소 크기 설정
        window.Show();
    }

    void OnGUI()
    {
        if (originalScript == null)
        {
            EditorGUILayout.LabelField("원본 스크립트가 선택되지 않았습니다.");
            return;
        }

        EditorGUILayout.LabelField($"**'{originalScript.name}' 관련 Partial 스크립트 관리**", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // ------------------ Partial 추가 섹션 ------------------
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
                // CollaborationScriptEditor의 static 메서드 호출
                if (CollaborationScriptEditor.CreateNewPartialScript(originalScript, newPartialFeatureName, newPartialAuthorName))
                {
                    newPartialFeatureName = ""; // 성공 시 필드 초기화
                    // Partial 스크립트가 추가되면 CollaborationScriptEditor의 Partial Count를 갱신
                    CollaborationScriptEditor editorWindow = GetWindow<CollaborationScriptEditor>();
                    if (editorWindow != null)
                    {
                        editorWindow.LoadScriptData();
                    }
                    AssetDatabase.Refresh();
                    Repaint(); // 현재 팝업 UI 업데이트
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
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (PartialScriptInfo info in filteredPartials)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField($"파일: {Path.GetFileName(info.partialFilePath)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"기능명: {info.featureName}"); // 기능명 표시
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

                // **새로운 스크립트 편집 버튼 추가**
                GUI.backgroundColor = Color.blue; // 파란색 버튼으로 설정
                if (GUILayout.Button("✏️ 스크립트 편집", GUILayout.Height(25)))
                {
                    // partialFilePath를 사용하여 MonoScript를 로드
                    MonoScript partialMonoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(info.partialFilePath);
                    if (partialMonoScript != null)
                    {
                        // MonoScript를 Open하는 것은 Unity의 기본 스크립트 에디터를 엽니다.
                        AssetDatabase.OpenAsset(partialMonoScript);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("오류", "Partial 스크립트 파일을 찾을 수 없습니다.", "확인");
                    }
                }
                GUI.backgroundColor = Color.white;
                // -------------------------------------------------

                // 메모 기능
                EditorGUILayout.LabelField("메모:");
                string newMemo = EditorGUILayout.TextArea(info.memo, GUILayout.Height(40));
                if (newMemo != info.memo)
                {
                    info.memo = newMemo;
                    PartialScriptManager.Instance.SetDirtyAndSave();
                }

                // To-Do 리스트 (CollaborationScriptEditor에서 가져옴)
                DrawTodoSection(info.todos, info, "partial_" + info.partialFilePath); // Unique ID for each partial's To-Do

                // 개별 통합 버튼
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button($"⬆️ '{Path.GetFileName(info.partialFilePath)}' 통합", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Partial 스크립트 통합", $"'{Path.GetFileName(info.partialFilePath)}' 내용을 '{originalScript.name}'에 통합하시겠습니까? 통합 후 Partial 스크립트 파일은 삭제됩니다.", "통합", "취소"))
                    {
                        CollaborationScriptEditor.IntegrateSelectedPartialScripts(originalScript, new List<PartialScriptInfo> { info });
                        // 통합 후 Partial 스크립트 목록 새로고침
                        Repaint();
                        // CollaborationScriptEditor의 데이터도 새로고침 (Partial Count 등)
                        CollaborationScriptEditor editorWindow = GetWindow<CollaborationScriptEditor>();
                        if (editorWindow != null)
                        {
                            editorWindow.LoadScriptData();
                        }
                    }
                }
                GUI.backgroundColor = Color.white;

                // 삭제 버튼
                GUI.backgroundColor = Color.red;
                if (GUILayout.Button("🗑️ 삭제", GUILayout.Height(25)))
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
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("닫기", GUILayout.Height(30)))
        {
            Close();
        }
    }

    // To-Do 섹션을 그리는 메서드 (ScriptCategoryAndMemoManager에서도 사용될 수 있도록 static으로 분리)
    // uniqueIdForInput은 텍스트 필드의 고유성을 위해 사용 (Partial 스크립트 경로 또는 원본 스크립트 경로)
    private void DrawTodoSection(List<TodoItem> todos, PartialScriptInfo partialInfo, string uniqueIdForInput)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        EditorGUILayout.LabelField("To-Do 목록", EditorStyles.boldLabel);

        // 진행 상태 표시
        int completedTodos = todos.Count(t => t.isCompleted);
        string todoStatus = $"{completedTodos}/{todos.Count} 완료";
        float progress = todos.Count > 0 ? (float)completedTodos / todos.Count : 0f;
        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight), progress, $"진행 상태: {todoStatus}");
        EditorGUILayout.Space(5);

        // 새 To-Do 추가 필드
        EditorGUILayout.BeginHorizontal();
        if (!newTodoDescriptions.ContainsKey(uniqueIdForInput))
        {
            newTodoDescriptions[uniqueIdForInput] = "";
        }
        newTodoDescriptions[uniqueIdForInput] = EditorGUILayout.TextField(newTodoDescriptions[uniqueIdForInput]);
        if (GUILayout.Button("➕ 추가", GUILayout.Width(60)))
        {
            string newTodoDesc = newTodoDescriptions[uniqueIdForInput];
            if (!string.IsNullOrWhiteSpace(newTodoDesc))
            {
                todos.Add(new TodoItem(newTodoDesc));
                PartialScriptManager.Instance.SetDirtyAndSave(); // PartialScriptManager 인스턴스를 통해 저장
                newTodoDescriptions[uniqueIdForInput] = "";
                Repaint(); // UI 업데이트
            }
        }
        EditorGUILayout.EndHorizontal();

        // To-Do 목록 표시
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.MinHeight(100), GUILayout.MaxHeight(200));
        for (int i = 0; i < todos.Count; i++)
        {
            EditorGUILayout.BeginHorizontal(GUI.skin.box);
            bool newIsCompleted = EditorGUILayout.Toggle(todos[i].isCompleted, GUILayout.Width(20));
            if (newIsCompleted != todos[i].isCompleted)
            {
                todos[i].isCompleted = newIsCompleted;
                PartialScriptManager.Instance.SetDirtyAndSave(); // PartialScriptManager 인스턴스를 통해 저장
                Repaint(); // UI 업데이트
            }
            EditorGUILayout.LabelField(todos[i].description, todos[i].isCompleted ? EditorStyles.miniLabel : EditorStyles.label);
            if (GUILayout.Button("🗑️", GUILayout.Width(25)))
            {
                todos.RemoveAt(i);
                PartialScriptManager.Instance.SetDirtyAndSave(); // PartialScriptManager 인스턴스를 통해 저장
                Repaint(); // UI 업데이트
                GUIUtility.ExitGUI(); // 삭제 후 즉시 GUI 종료하여 오류 방지
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }


    // CollaborationScriptEditor에서 복사해온 활성화/비활성화 로직
    private void TogglePartialScriptActiveState(PartialScriptInfo info)
    {
        string oldPath = info.partialFilePath;
        string newPath;

        if (Path.GetExtension(oldPath) == ".cs") // 현재 활성화 상태 -> 비활성화로 변경
        {
            newPath = Path.ChangeExtension(oldPath, ".disabled");
        }
        else // 현재 비활성화 상태 -> 활성화로 변경
        {
            newPath = Path.ChangeExtension(oldPath, ".cs");
        }

        string result = AssetDatabase.MoveAsset(oldPath, newPath);
        if (string.IsNullOrEmpty(result))
        {
            PartialScriptManager.Instance.UpdatePartialScriptPath(oldPath, newPath);
            Debug.Log($"Partial 스크립트 상태 변경: {oldPath} -> {newPath}");
            AssetDatabase.Refresh();
            // UI를 새로고침하기 위해 창을 Repaint
            Repaint();
            // CollaborationScriptEditor의 데이터도 새로고침 (Partial Count 등)
            CollaborationScriptEditor window = GetWindow<CollaborationScriptEditor>();
            window.LoadScriptData();
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
        Repaint(); // UI 업데이트
        // CollaborationScriptEditor의 데이터도 새로고침 (Partial Count 등)
        CollaborationScriptEditor window = GetWindow<CollaborationScriptEditor>();
        if (window != null)
        {
            window.LoadScriptData();
        }
    }
}