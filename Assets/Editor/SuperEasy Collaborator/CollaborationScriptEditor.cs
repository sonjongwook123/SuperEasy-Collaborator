// CollaborationScriptEditor.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System;
using System.Linq;
using System.Collections.Generic;

// Roslyn 네임스페이스 추가
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text.RegularExpressions; // Regex를 위해 추가

public class CollaborationScriptEditor : EditorWindow
{
    private string authorName = "";
    private Vector2 mainScrollPos;
    private Vector2 categoryScrollPos;
    private int selectedTab = 0; // 0: 스크립트, 1: Partial, 2: 카테고리 관리

    // 로딩 최적화를 위한 캐시
    private List<MonoScript> cachedOriginalScripts;
    private Dictionary<string, ScriptMetadata> cachedScriptMetadata;
    private Dictionary<string, int> cachedPartialCounts; // 각 원본 스크립트의 Partial 개수 캐시

    // Partial 추가 시 사용할 기본 작성자 이름 (팝업에 전달)
    private string currentAuthorNameForNewPartial = "";

    // 페이징 관련 변수
    private int currentPage = 0;
    private const int scriptsPerPage = 10;

    // 카테고리 필터링 관련 변수
    private string selectedCategoryFilter = "전체보기"; // 초기 필터: 전체보기

    // 배너 이미지 변수
    private Texture2D bannerImage;
    private const int bannerWidth = 1000;
    private const int bannerHeight = 112;


    [MenuItem("Tools/Collaboration Script Editor")]
    public static void ShowWindow()
    {
        GetWindow<CollaborationScriptEditor>("협업 스크립트 에디터");
    }

    void OnEnable()
    {
        // 초기 로딩 (에디터 활성화 시 한 번)
        LoadScriptData();
        // 기본 만든이 이름 로드
        authorName = EditorPrefs.GetString("CollaborationScriptEditor.AuthorName", Environment.UserName);
        currentAuthorNameForNewPartial = authorName; // 초기값 설정

        // 배너 이미지 로드
        LoadBannerImage();
    }

    // Window가 포커스를 얻거나 프로젝트 변경이 감지될 때 데이터 새로고침
    void OnFocus()
    {
        LoadScriptData();
    }

    public void LoadScriptData()
    {
        // 모든 MonoScript GUID 찾기
        string[] scriptGuids = AssetDatabase.FindAssets("t:MonoScript");
        
        cachedOriginalScripts = new List<MonoScript>();
        cachedScriptMetadata = new Dictionary<string, ScriptMetadata>();
        cachedPartialCounts = new Dictionary<string, int>();

        // Partial 파일 확장자
        string[] partialExtensions = { ".cs.partial", ".disabled" };

        foreach (string guid in scriptGuids)
        {
            string scriptPath = AssetDatabase.GUIDToAssetPath(guid);

            // Editor 스크립트 및 Partial 스크립트 제외 (여기서 Editor 스크립트 필터링)
            if (scriptPath.Contains("Assets/Editor/") &&
                !scriptPath.Contains("PartialScriptManager.cs") && // 핵심 스크립트 제외
                !scriptPath.Contains("ScriptCategoryAndMemoManager.cs") && // 핵심 스크립트 제외
                !scriptPath.Contains("CollaborationScriptEditor.cs") && // 핵심 스크립트 제외
                !scriptPath.Contains("PartialScriptPopup.cs") && // 핵심 스크립트 제외
                !scriptPath.Contains("OriginalScriptTodoPopup.cs") // 핵심 스크립트 제외
               )
            {
                continue; // Editor 폴더 내의 다른 스크립트는 건너뜀
            }

            // .cs.partial 또는 .disabled 확장자를 가진 파일은 원본 스크립트 목록에서 제외
            if (partialExtensions.Any(ext => scriptPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            // 스크립트가 유효하고, 실제로 파일이 존재하는지 확인하여 목록에 추가
            if (script != null && File.Exists(scriptPath))
            {
                cachedOriginalScripts.Add(script);
                ScriptMetadata metadata = ScriptCategoryAndMemoManager.Instance.GetOrCreateScriptMetadata(scriptPath);
                cachedScriptMetadata[scriptPath] = metadata;
            }
            // else { Debug.LogWarning($"Skipping non-existent or invalid script asset: {scriptPath}"); } // 디버깅용
        }

        // PartialScriptManager에서 각 원본 스크립트의 Partial 개수 계산
        foreach (var originalScript in cachedOriginalScripts)
        {
            string originalScriptPath = AssetDatabase.GetAssetPath(originalScript);
            int count = PartialScriptManager.Instance.partialScripts
                .Count(p => p.originalScriptPath == originalScriptPath);
            cachedPartialCounts[originalScriptPath] = count;
        }

        // 이름순 정렬
        cachedOriginalScripts = cachedOriginalScripts.OrderBy(s => s.name).ToList();

        // Debug.Log("Script data loaded/refreshed."); // 로딩 확인용
    }

    private void LoadBannerImage()
    {
        // CollaborationScriptEditor.cs 스크립트가 있는 폴더 경로
        string editorScriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
        string editorFolderPath = Path.GetDirectoryName(editorScriptPath);
        string bannerPath = Path.Combine(editorFolderPath, "banner.png").Replace("\\", "/");

        bannerImage = AssetDatabase.LoadAssetAtPath<Texture2D>(bannerPath);
        if (bannerImage == null)
        {
            Debug.LogWarning($"Banner image not found at: {bannerPath}. Please ensure banner.png is in the same folder as CollaborationScriptEditor.cs");
        }
    }


    void OnGUI()
    {
        // 배너 이미지 표시
        if (bannerImage != null)
        {
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // 중앙 정렬
            // 배너 이미지 크기 강제 설정
            GUILayout.Label(bannerImage, GUILayout.Width(bannerWidth), GUILayout.Height(bannerHeight));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
        }

        mainScrollPos = EditorGUILayout.BeginScrollView(mainScrollPos);

        GUILayout.Label("협업 스크립트 에디터", EditorStyles.largeLabel);

        EditorGUILayout.Space(10);

        authorName = EditorGUILayout.TextField("나의 이름:", authorName);
        if (GUI.changed)
        {
            EditorPrefs.SetString("CollaborationScriptEditor.AuthorName", authorName);
            currentAuthorNameForNewPartial = authorName; // 현재 저자 이름 업데이트
        }

        EditorGUILayout.Space(10);

        selectedTab = GUILayout.Toolbar(selectedTab, new string[] { "스크립트 관리", "Partial 스크립트", "카테고리 관리" });

        EditorGUILayout.Space(10);

        switch (selectedTab)
        {
            case 0:
                DrawScriptManagementTab();
                break;
            case 1:
                DrawPartialScriptTab();
                break;
            case 2:
                DrawCategoryManagementTab();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawScriptManagementTab()
    {
        EditorGUILayout.LabelField("전체 스크립트 관리 (메모, 카테고리, To-Do)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (cachedOriginalScripts == null || cachedOriginalScripts.Count == 0)
        {
            EditorGUILayout.HelpBox("프로젝트에 스크립트가 없습니다.", MessageType.Info);
            return;
        }

        // 카테고리 필터 버튼
        DrawCategoryFilterButtons();

        EditorGUILayout.Space();

        // 필터링된 스크립트 목록 가져오기
        List<MonoScript> filteredScripts = GetFilteredScripts();

        if (filteredScripts.Count == 0)
        {
            EditorGUILayout.HelpBox($"'{selectedCategoryFilter}' 카테고리에 해당하는 스크립트가 없습니다.", MessageType.Info);
            // 페이징 컨트롤은 필터된 스크립트가 있을 때만 보이도록 합니다.
        }
        else
        {
            // 페이징 처리
            int totalPages = Mathf.CeilToInt((float)filteredScripts.Count / scriptsPerPage);
            currentPage = Mathf.Clamp(currentPage, 0, totalPages - 1); // 현재 페이지 유효성 검사

            int startIndex = currentPage * scriptsPerPage;
            int endIndex = Mathf.Min(startIndex + scriptsPerPage, filteredScripts.Count);

            for (int i = startIndex; i < endIndex; i++)
            {
                MonoScript script = filteredScripts[i];
                string scriptPath = AssetDatabase.GetAssetPath(script);
                ScriptMetadata metadata = cachedScriptMetadata[scriptPath];

                EditorGUILayout.BeginVertical(GUI.skin.box);
                // 스크립트 이름 예쁘게 표시 및 경로 표시
                EditorGUILayout.LabelField($"{script.name}.cs", EditorStyles.boldLabel); // 스크립트 이름
                EditorGUILayout.LabelField($"경로: {scriptPath}", EditorStyles.miniLabel); // 스크립트 경로 (작은 글씨)

                // Partial 개수 표시
                int partialCount = cachedPartialCounts.ContainsKey(scriptPath) ? cachedPartialCounts[scriptPath] : 0;
                EditorGUILayout.LabelField($"Partial 파일 개수: {partialCount}개");

                // To-Do 달성목록 체력바 (ProgressBar)
                DrawTodoProgressBar(metadata.todos);

                EditorGUILayout.Space(5);

                EditorGUILayout.BeginHorizontal();

                // To-Do 보기 버튼
                if (GUILayout.Button($"📋 To-Do 보기 ({metadata.todos.Count})", GUILayout.Height(25)))
                {
                    OriginalScriptTodoPopup.ShowWindow(script, metadata);
                }

                // Partial 목록 버튼
                GUI.backgroundColor = Color.yellow;
                if (GUILayout.Button($"💡 '{script.name}' Partial 목록 ({partialCount})", GUILayout.Height(25)))
                {
                    // PartialScriptPopup에 현재 저자 이름을 넘겨주어, Partial 추가 시 기본값으로 사용
                    PartialScriptPopup.ShowPartialListForScript(script, currentAuthorNameForNewPartial);
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space(5);

                // 카테고리 선택
                int currentCategoryIndex = ScriptCategoryAndMemoManager.Instance.categories.IndexOf(metadata.category);
                if (currentCategoryIndex == -1) currentCategoryIndex = 0; // Uncategorized (또는 기본값)

                int newCategoryIndex = EditorGUILayout.Popup("카테고리:", currentCategoryIndex, ScriptCategoryAndMemoManager.Instance.categories.ToArray());
                if (newCategoryIndex != currentCategoryIndex)
                {
                    metadata.category = ScriptCategoryAndMemoManager.Instance.categories[newCategoryIndex];
                    ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
                }

                // 메모 기능
                EditorGUILayout.LabelField("메모:");
                string newMemo = EditorGUILayout.TextArea(metadata.memo, GUILayout.Height(40));
                if (newMemo != metadata.memo)
                {
                    metadata.memo = newMemo;
                    ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            // 페이징 컨트롤 UI
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.enabled = (currentPage > 0);
            if (GUILayout.Button("◀ 이전", GUILayout.Width(70)))
            {
                currentPage--;
            }
            GUI.enabled = true;
            EditorGUILayout.LabelField($"페이지 {currentPage + 1} / {totalPages}", EditorStyles.boldLabel, GUILayout.Width(100));
            GUI.enabled = (currentPage < totalPages - 1);
            if (GUILayout.Button("다음 ▶", GUILayout.Width(70)))
            {
                currentPage++;
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawCategoryFilterButtons()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        // 전체보기 버튼
        GUI.backgroundColor = (selectedCategoryFilter == "전체보기") ? Color.cyan : Color.white;
        if (GUILayout.Button("전체보기", EditorStyles.toolbarButton))
        {
            selectedCategoryFilter = "전체보기";
            currentPage = 0; // 필터 변경 시 첫 페이지로 이동
        }
        GUI.backgroundColor = Color.white;

        // 각 카테고리 버튼
        foreach (string category in ScriptCategoryAndMemoManager.Instance.categories)
        {
            GUI.backgroundColor = (selectedCategoryFilter == category) ? Color.cyan : Color.white;
            if (GUILayout.Button(category, EditorStyles.toolbarButton))
            {
                selectedCategoryFilter = category;
                currentPage = 0; // 필터 변경 시 첫 페이지로 이동
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();
    }

    private List<MonoScript> GetFilteredScripts()
    {
        if (selectedCategoryFilter == "전체보기")
        {
            return cachedOriginalScripts;
        }
        else
        {
            return cachedOriginalScripts
                .Where(script => {
                    string scriptPath = AssetDatabase.GetAssetPath(script);
                    return cachedScriptMetadata.ContainsKey(scriptPath) &&
                           cachedScriptMetadata[scriptPath].category == selectedCategoryFilter;
                })
                .ToList();
        }
    }


    private void DrawPartialScriptTab()
    {
        EditorGUILayout.LabelField("전체 Partial 스크립트 목록 및 관리", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (PartialScriptManager.Instance.partialScripts.Count == 0)
        {
            EditorGUILayout.HelpBox("현재 등록된 Partial 스크립트가 없습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"총 Partial 스크립트: {PartialScriptManager.Instance.partialScripts.Count}개", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        foreach (var info in PartialScriptManager.Instance.partialScripts.OrderBy(p => Path.GetFileName(p.partialFilePath)))
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"**Partial File:** {Path.GetFileName(info.partialFilePath)}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"**원본:** {Path.GetFileName(info.originalScriptPath)}");
            EditorGUILayout.LabelField($"**기능:** {info.featureName}"); // 담당 기능 이름 표시
            EditorGUILayout.LabelField($"**작성자:** {info.authorName}");
            EditorGUILayout.LabelField($"**생성일:** {info.creationDate}");

            bool isActive = Path.GetExtension(info.partialFilePath).Equals(".cs", StringComparison.OrdinalIgnoreCase);
            string buttonText = isActive ? "🔴 비활성화" : "🟢 활성화";
            GUI.backgroundColor = isActive ? Color.red : Color.green;

            if (GUILayout.Button(buttonText))
            {
                TogglePartialScriptActiveState(info);
            }
            GUI.backgroundColor = Color.white;

            // 제거 버튼
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
            LoadScriptData(); // 데이터 새로고침
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
        LoadScriptData(); // 데이터 새로고침
    }


    private void DrawCategoryManagementTab()
    {
        EditorGUILayout.LabelField("카테고리 목록 관리", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 기존 카테고리 표시 및 삭제
        EditorGUILayout.LabelField("기존 카테고리:");
        categoryScrollPos = EditorGUILayout.BeginScrollView(categoryScrollPos, GUILayout.Height(150));
        for (int i = 0; i < ScriptCategoryAndMemoManager.Instance.categories.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(ScriptCategoryAndMemoManager.Instance.categories[i]);
            if (ScriptCategoryAndMemoManager.Instance.categories[i] != "Uncategorized" && GUILayout.Button("삭제", GUILayout.Width(50)))
            {
                // 삭제 시 해당 카테고리에 속한 스크립트를 "Uncategorized"로 변경
                string categoryToDelete = ScriptCategoryAndMemoManager.Instance.categories[i];
                foreach (var metadata in ScriptCategoryAndMemoManager.Instance.scriptMetadataList)
                {
                    if (metadata.category == categoryToDelete)
                    {
                        metadata.category = "Uncategorized";
                    }
                }
                ScriptCategoryAndMemoManager.Instance.categories.RemoveAt(i);
                ScriptCategoryAndMemoManager.Instance.SetDirtyAndSave();
                LoadScriptData(); // 데이터 새로고침
                GUIUtility.ExitGUI(); // 삭제 후 즉시 GUI 종료하여 오류 방지
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.Space();

        // 새 카테고리 추가
        EditorGUILayout.LabelField("새 카테고리 추가:");
        EditorGUILayout.BeginHorizontal();
        string newCategoryName = EditorGUILayout.TextField("", "", GUILayout.Width(200));
        if (GUILayout.Button("추가", GUILayout.Width(50)))
        {
            ScriptCategoryAndMemoManager.Instance.AddCategory(newCategoryName);
            LoadScriptData(); // 데이터 새로고침
        }
        EditorGUILayout.EndHorizontal();
    }


    // To-Do 진행률 바를 그리는 헬퍼 함수
    private void DrawTodoProgressBar(List<TodoItem> todos)
    {
        int completedTodos = todos.Count(t => t.isCompleted);
        int totalTodos = todos.Count;
        string todoStatus = totalTodos > 0 ? $"({completedTodos}/{totalTodos})" : "(0/0)";
        float progress = totalTodos > 0 ? (float)completedTodos / totalTodos : 0f;

        Rect progressBarRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
        EditorGUI.ProgressBar(progressBarRect, progress, $"To-Do 진행: {todoStatus}");
        EditorGUILayout.Space(5);
    }

    // ------------ Roslyn 기반 Partial 스크립트 통합 로직 ----------------

    // 팝업에서 호출할 수 있도록 static으로 변경
    public static bool CreateNewPartialScript(MonoScript originalScript, string featureName, string authorName)
    {
        if (originalScript == null)
        {
            EditorUtility.DisplayDialog("오류", "Partial 스크립트를 생성할 원본 스크립트가 선택되지 않았습니다.", "확인");
            return false;
        }

        string originalScriptPath = AssetDatabase.GetAssetPath(originalScript);
        string originalFileName = Path.GetFileNameWithoutExtension(originalScriptPath);
        string originalFolderPath = Path.GetDirectoryName(originalScriptPath);

        // 기능 이름을 포함한 새 Partial 파일명 생성
        string partialFileName = $"{originalFileName}.{featureName.Replace(" ", "").Replace("-", "")}.partial.cs"; // 파일명에 특수문자 제거
        string partialFilePath = Path.Combine(originalFolderPath, partialFileName).Replace("\\", "/");

        if (File.Exists(partialFilePath))
        {
            EditorUtility.DisplayDialog("오류", $"'{partialFileName}' 파일이 이미 존재합니다. 다른 기능 이름을 사용해주세요.", "확인");
            return false;
        }

        // Partial 스크립트 내용 생성
        string initialContent = GeneratePartialScriptContent(originalScript, featureName, authorName);

        try
        {
            File.WriteAllText(partialFilePath, initialContent);
            AssetDatabase.ImportAsset(partialFilePath);

            string creationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            PartialScriptManager.Instance.AddPartialScript(partialFilePath, originalScriptPath, featureName, authorName, creationDate);

            Debug.Log($"Partial 스크립트 '{partialFileName}'가 성공적으로 생성되었습니다.");
            // CollaborationScriptEditor의 데이터 새로고침
            CollaborationScriptEditor window = GetWindow<CollaborationScriptEditor>();
            window.LoadScriptData();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Partial 스크립트 생성 중 오류 발생: {e.Message}");
            EditorUtility.DisplayDialog("오류", $"Partial 스크립트 생성 중 오류 발생: {e.Message}", "확인");
            return false;
        }
    }


    private static string GeneratePartialScriptContent(MonoScript originalScript, string featureName, string authorName)
    {
        string originalCode = originalScript.text;
        SyntaxTree tree = CSharpSyntaxTree.ParseText(originalCode);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        // 원본 스크립트의 네임스페이스와 클래스 이름 추출
        string originalNamespace = "";
        NamespaceDeclarationSyntax namespaceDeclaration = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        if (namespaceDeclaration != null)
        {
            originalNamespace = namespaceDeclaration.Name.ToString();
        }

        ClassDeclarationSyntax originalClass = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        string originalClassName = originalClass?.Identifier.Text;

        if (string.IsNullOrEmpty(originalClassName))
        {
            // 클래스를 찾지 못했거나 이름이 없는 경우, 기본값 사용 또는 오류 처리
            originalClassName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(originalScript));
        }


        // Partial 클래스 선언 생성
        string partialClassDeclaration = $"public partial class {originalClassName}";

        // 새 partial 스크립트 내용 구성
        string content = "";

        if (!string.IsNullOrEmpty(originalNamespace))
        {
            content += $"namespace {originalNamespace}\n{{\n";
        }

        content += $@"
// Partial Script for: {originalScript.name}.cs
// Feature: {featureName}
// Author: {authorName}
// Date: {DateTime.Now:yyyy-MM-dd HH:mm}

{partialClassDeclaration}
{{
    // TODO: {featureName} 기능 구현
    /*
    private void Example{featureName.Replace(" ", "").Replace("-", "")}Method()
    {{
        // 이 곳에 {featureName} 기능과 관련된 코드를 작성하세요.
    }}
    */
}}
";
        if (!string.IsNullOrEmpty(originalNamespace))
        {
            content += "}\n";
        }

        return content;
    }

    // 팝업에서 호출할 수 있도록 static으로 변경
    public static bool IntegrateSelectedPartialScripts(List<PartialScriptInfo> partialsToIntegrate)
    {
        if (partialsToIntegrate == null || partialsToIntegrate.Count == 0)
        {
            EditorUtility.DisplayDialog("알림", "통합할 Partial 스크립트가 없습니다.", "확인");
            return false;
        }

        // 통합할 원본 스크립트 경로가 모두 동일한지 확인 (혹시 모를 오류 방지)
        string originalScriptPath = partialsToIntegrate.First().originalScriptPath;
        if (partialsToIntegrate.Any(p => p.originalScriptPath != originalScriptPath))
        {
            EditorUtility.DisplayDialog("오류", "선택된 Partial 스크립트들이 서로 다른 원본 스크립트에 연결되어 있습니다. 각각 통합해주세요.", "확인");
            return false;
        }

        MonoScript originalScript = AssetDatabase.LoadAssetAtPath<MonoScript>(originalScriptPath);
        if (originalScript == null)
        {
            EditorUtility.DisplayDialog("오류", $"원본 스크립트 '{originalScriptPath}'를 찾을 수 없습니다.", "확인");
            return false;
        }

        string originalCode = originalScript.text;
        SyntaxTree originalTree = CSharpSyntaxTree.ParseText(originalCode);
        CompilationUnitSyntax originalRoot = originalTree.GetCompilationUnitRoot();
        ClassDeclarationSyntax originalClass = originalRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

        if (originalClass == null)
        {
            EditorUtility.DisplayDialog("오류", "원본 스크립트에서 클래스 정의를 찾을 수 없습니다.", "확인");
            return false;
        }

        ClassDeclarationSyntax newOriginalClass = originalClass;
        bool anyMembersAdded = false;
        List<string> conflictMessages = new List<string>(); // 충돌 멤버 메시지 저장

        foreach (var info in partialsToIntegrate)
        {
            if (!File.Exists(info.partialFilePath))
            {
                Debug.LogWarning($"Partial 스크립트 파일 '{info.partialFilePath}'를 찾을 수 없습니다. 통합 목록에서 건너뜁니다.");
                continue;
            }

            string partialCode = File.ReadAllText(info.partialFilePath);
            SyntaxTree partialTree = CSharpSyntaxTree.ParseText(partialCode);
            CompilationUnitSyntax partialRoot = partialTree.GetCompilationUnitRoot();
            ClassDeclarationSyntax partialClass = partialRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            if (partialClass == null)
            {
                Debug.LogWarning($"Partial 스크립트 '{Path.GetFileName(info.partialFilePath)}'에서 클래스 정의를 찾을 수 없습니다. 통합할 멤버가 없습니다.");
                continue;
            }

            // Partial 클래스의 모든 멤버 (메서드, 필드, 프로퍼티 등)를 순회
            foreach (var member in partialClass.Members)
            {
                // 이미 원본 클래스에 같은 이름과 시그니처의 멤버가 있는지 확인
                // 이 부분은 실제 프로젝트의 복잡성에 따라 더 정교한 비교 로직이 필요할 수 있습니다.
                // 현재는 단순 이름 비교
                bool conflict = false;
                if (member is MethodDeclarationSyntax method)
                {
                    if (originalClass.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == method.Identifier.Text && m.ParameterList.ToString() == method.ParameterList.ToString()))
                    {
                        conflict = true;
                        conflictMessages.Add($"메서드 '{method.Identifier.Text}'");
                    }
                }
                else if (member is FieldDeclarationSyntax field)
                {
                    if (originalClass.Members.OfType<FieldDeclarationSyntax>().Any(f => f.Declaration.Variables.Any(v => v.Identifier.Text == field.Declaration.Variables.First().Identifier.Text)))
                    {
                        conflict = true;
                        conflictMessages.Add($"필드 '{field.Declaration.Variables.First().Identifier.Text}'");
                    }
                }
                else if (member is PropertyDeclarationSyntax property)
                {
                    if (originalClass.Members.OfType<PropertyDeclarationSyntax>().Any(p => p.Identifier.Text == property.Identifier.Text))
                    {
                        conflict = true;
                        conflictMessages.Add($"프로퍼티 '{property.Identifier.Text}'");
                    }
                }
                // 다른 멤버 타입 (Event, Delegate 등)도 필요하면 여기에 추가

                if (!conflict)
                {
                    newOriginalClass = newOriginalClass.AddMembers(member);
                    anyMembersAdded = true;
                }
            }

            // 통합 완료된 Partial 스크립트 파일 삭제 및 PartialScriptManager에서 정보 제거
            // 통합 성공 시에만 삭제
            if (anyMembersAdded) // 실제 멤버가 추가되었을 때만 삭제
            {
                if (File.Exists(info.partialFilePath))
                {
                    AssetDatabase.DeleteAsset(info.partialFilePath);
                }
                PartialScriptManager.Instance.RemovePartialScript(info.partialFilePath);
            }
        }

        // 원본 Document의 Root를 새롭게 변경된 클래스로 교체
        SyntaxNode newRoot = originalRoot.ReplaceNode(originalClass, newOriginalClass);

        string newOriginalCode = newRoot.NormalizeWhitespace().ToFullString(); // 깔끔하게 포맷

        File.WriteAllText(originalScriptPath, newOriginalCode);
        AssetDatabase.ImportAsset(originalScriptPath); // 변경된 원본 스크립트 다시 임포트

        if (conflictMessages.Count > 0)
        {
            Debug.LogWarning($"Partial 스크립트 통합 완료. 다음 멤버들은 원본에 이미 존재하여 무시되었습니다: {string.Join(", ", conflictMessages)}");
        }
        else if (anyMembersAdded)
        {
            Debug.Log($"선택된 Partial 스크립트 내용들을 '{Path.GetFileName(originalScriptPath)}'에 성공적으로 통합했습니다.");
        }
        else
        {
            Debug.Log($"선택된 Partial 스크립트들은 통합할 새로운 멤버가 없었습니다.");
        }
        
        // 통합 후 데이터 새로고침
        CollaborationScriptEditor window = GetWindow<CollaborationScriptEditor>();
        window.LoadScriptData();

        return true;
    }
}