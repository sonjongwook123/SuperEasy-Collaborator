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
    private Vector2 newTodoScrollPos; // Separate scroll for To-Do section (kept for potential future use or if other parts of the system still use it)
    private Vector2 partialListScrollPos; // Separate scroll for the list of partial scripts

    private Dictionary<string, string> newTodoDescriptions = new Dictionary<string, string>();

    // Partial 추가를 위한 필드
    private string newPartialFeatureName = "";
    private string newPartialAuthorName = ""; // CollaborationScriptEditor에서 전달받은 기본값

    // 페이징 관련 변수
    private int currentPagePartial = 0;
    private const int partialsPerPage = 3; // 한 페이지에 표시할 Partial 스크립트 수

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
            // Paging calculation
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

                // To-Do 리스트 (Partial 리스트에서 제거됨)
                // DrawTodoSection(info.todos, "partial_" + info.partialFilePath); // 이 줄을 제거했습니다.

                // 개별 통합 버튼
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button($"⬆️ '{Path.GetFileName(info.partialFilePath)}' 통합", GUILayout.Height(25)))
                {
                    if (EditorUtility.DisplayDialog("Partial 스크립트 통합", $"'{Path.GetFileName(info.partialFilePath)}' 내용을 '{originalScript.name}'에 통합하시겠습니까? 통합 후 Partial 스크립트 파일은 삭제됩니다.", "통합", "취소"))
                    {
                        // CollaborationScriptEditor.IntegrateSelectedPartialScripts 메서드 호출 시
                        // 내부적으로 PartialScriptManager.Instance.RemovePartialScript(info.partialFilePath);
                        // 및 AssetDatabase.DeleteAsset(info.partialFilePath); 가 호출됨.
                        // 이 팝업의 목록에서도 사라지고, 파일도 삭제됩니다.
                        if (CollaborationScriptEditor.IntegrateSelectedPartialScripts(originalScript, new List<PartialScriptInfo> { info }))
                        {
                            // 통합 성공 시 데이터 새로고침 및 UI 업데이트
                            Repaint();
                            CollaborationScriptEditor editorWindow = GetWindow<CollaborationScriptEditor>();
                            if (editorWindow != null)
                            {
                                editorWindow.LoadScriptData(); // CollaborationScriptEditor의 Partial Count 갱신
                            }
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

            // Paging UI
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

    // To-Do 섹션을 그리는 메서드 (PartialScriptPopup에서는 더 이상 사용되지 않음)
    private void DrawTodoSection(List<TodoItem> todos, string uniqueIdForInput)
    {
        // 이 메서드는 PartialScriptPopup에서 제거되었지만, CollaborationScriptEditor에서 사용될 수 있습니다.
        // CollaborationScriptEditor의 DrawTodoSection 메서드를 참조하세요.
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
        Repaint(); // UI 업데이트
        // CollaborationScriptEditor의 데이터도 새로고침 (Partial Count 등)
        CollaborationScriptEditor window = GetWindow<CollaborationScriptEditor>();
        if (window != null)
        {
            window.LoadScriptData();
        }
    }
}