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
                    newPartialFeatureName = ""; // 추가 성공 시 필드 초기화
                    // 창 데이터 새로고침 (PartialScriptManager의 정보가 업데이트됨)
                    Repaint(); // UI 업데이트를 위해 Repaint 호출
                }
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();
        // ----------------------------------------------------

        List<PartialScriptInfo> filteredPartials = PartialScriptManager.Instance.partialScripts
            .Where(p => p.originalScriptPath == AssetDatabase.GetAssetPath(originalScript))
            .ToList();

        // ------------------ 모두 합치기 버튼 ------------------
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("✨ 모든 Partial 스크립트 통합 (원본 스크립트로 합치기)", GUILayout.Height(40)))
        {
            if (filteredPartials.Count == 0)
            {
                EditorUtility.DisplayDialog("알림", "통합할 Partial 스크립트가 없습니다.", "확인");
            }
            else if (EditorUtility.DisplayDialog("통합 확인",
                                                $"'{originalScript.name}'와 관련된 모든 Partial 스크립트 ({filteredPartials.Count}개)를 원본 스크립트로 통합하시겠습니까? 통합된 Partial 스크립트 파일은 삭제됩니다.",
                                                "통합", "취소"))
            {
                if (CollaborationScriptEditor.IntegrateSelectedPartialScripts(filteredPartials))
                {
                    // 통합 성공 후 창 닫기 또는 UI 새로고침
                    // CollaborationScriptEditor.IntegrateSelectedPartialScripts에서 LoadScriptData 호출하므로 여기서 별도 로딩 불필요
                    Close(); // 팝업 닫기
                }
            }
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.Space();
        // ----------------------------------------------------


        EditorGUILayout.LabelField("현재 연결된 Partial 스크립트 목록:", EditorStyles.boldLabel);
        if (filteredPartials.Count == 0)
        {
            EditorGUILayout.HelpBox($"'{originalScript.name}'에 연결된 Partial 스크립트가 없습니다.", MessageType.Info);
        }
        else
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            foreach (var info in filteredPartials)
            {
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField($"**Partial File:** {Path.GetFileName(info.partialFilePath)}", EditorStyles.boldLabel);
                EditorGUILayout.LabelField($"**담당 기능:** {info.featureName}"); // 새 필드 표시
                EditorGUILayout.LabelField($"**작성자:** {info.authorName}");
                EditorGUILayout.LabelField($"**생성일:** {info.creationDate}");

                // 활성화/비활성화 버튼
                bool isActive = Path.GetExtension(info.partialFilePath).Equals(".cs", StringComparison.OrdinalIgnoreCase);
                string buttonText = isActive ? "🔴 비활성화" : "🟢 활성화";
                GUI.backgroundColor = isActive ? Color.red : Color.green;

                if (GUILayout.Button(buttonText))
                {
                    TogglePartialScriptActiveState(info);
                }
                GUI.backgroundColor = Color.white;

                // 메모 기능
                EditorGUILayout.LabelField("메모:");
                string newMemo = EditorGUILayout.TextArea(info.memo, GUILayout.Height(40));
                if (newMemo != info.memo)
                {
                    info.memo = newMemo;
                    PartialScriptManager.Instance.SetDirtyAndSave();
                }

                // To-Do 리스트 (CollaborationScriptEditor에서 가져옴)
                DrawTodoListUI(info.todos, info.partialFilePath, PartialScriptManager.Instance);

                // 개별 통합 버튼
                if (GUILayout.Button("➡️ 이 Partial만 통합 (파일 삭제)", GUILayout.Width(250)))
                {
                    if (EditorUtility.DisplayDialog("개별 통합 확인",
                                                    $"Partial 스크립트 '{Path.GetFileName(info.partialFilePath)}'를 원본 스크립트로 통합하시겠습니까? 통합된 Partial 스크립트 파일은 삭제됩니다.",
                                                    "통합", "취소"))
                    {
                        if (CollaborationScriptEditor.IntegrateSelectedPartialScripts(new List<PartialScriptInfo> { info }))
                        {
                            Repaint(); // UI 업데이트
                        }
                    }
                }

                // 제거 버튼 (파일 삭제)
                if (GUILayout.Button("🗑️ 제거 (파일 삭제)", GUILayout.Width(150)))
                {
                    if (EditorUtility.DisplayDialog("Partial 제거 확인",
                                                    $"Partial 스크립트 '{Path.GetFileName(info.partialFilePath)}'를 정말 제거하시겠습니까? 관련 파일도 함께 삭제됩니다.",
                                                    "제거", "취소"))
                    {
                        DeletePartialScriptFileAndInfo(info);
                    }
                }


                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
            EditorGUILayout.EndScrollView();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("닫기", GUILayout.Height(30)))
        {
            Close();
        }
    }

    // CollaborationScriptEditor에서 복사해온 To-Do UI 그리기 함수
    private void DrawTodoListUI(List<TodoItem> todos, string uniqueIdForInput, ScriptableObject managerToSave)
    {
        EditorGUILayout.LabelField("To-Do List:", EditorStyles.boldLabel);

        int completedTodos = todos.Count(t => t.isCompleted);
        int totalTodos = todos.Count;
        string todoStatus = totalTodos > 0 ? $"({completedTodos}/{totalTodos})" : "(0/0)";
        float progress = totalTodos > 0 ? (float)completedTodos / totalTodos : 0f;

        Rect progressBarRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        EditorGUI.ProgressBar(progressBarRect, progress, $"진행 상태: {todoStatus}");
        EditorGUILayout.Space(5);

        for (int i = 0; i < todos.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            bool newIsCompleted = EditorGUILayout.Toggle(todos[i].isCompleted, GUILayout.Width(20));
            if (newIsCompleted != todos[i].isCompleted)
            {
                todos[i].isCompleted = newIsCompleted;
                EditorUtility.SetDirty(managerToSave);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.LabelField(todos[i].description, todos[i].isCompleted ? EditorStyles.miniLabel : EditorStyles.label);
            if (GUILayout.Button("🗑️", GUILayout.Width(25)))
            {
                todos.RemoveAt(i);
                EditorUtility.SetDirty(managerToSave);
                AssetDatabase.SaveAssets();
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }

        if (!newTodoDescriptions.ContainsKey(uniqueIdForInput))
        {
            newTodoDescriptions[uniqueIdForInput] = "";
        }
        EditorGUILayout.BeginHorizontal();
        newTodoDescriptions[uniqueIdForInput] = EditorGUILayout.TextField("새 To-Do:", newTodoDescriptions[uniqueIdForInput]);
        if (GUILayout.Button("➕ 추가", GUILayout.Width(60)))
        {
            string newTodoDesc = newTodoDescriptions[uniqueIdForInput];
            if (!string.IsNullOrWhiteSpace(newTodoDesc))
            {
                todos.Add(new TodoItem(newTodoDesc));
                EditorUtility.SetDirty(managerToSave);
                AssetDatabase.SaveAssets();
                newTodoDescriptions[uniqueIdForInput] = "";
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void TogglePartialScriptActiveState(PartialScriptInfo info)
    {
        string oldPath = info.partialFilePath;
        bool currentlyActive = Path.GetExtension(oldPath).Equals(".cs", StringComparison.OrdinalIgnoreCase);
        string newPath;

        if (currentlyActive)
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
        window.LoadScriptData();
    }
}